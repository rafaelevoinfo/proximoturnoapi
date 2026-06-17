# Especificação de Design: Campo Ativo e Inativação de Contratos em Atualização de Pedidos

## Histórico de Revisões
- **Data:** 16/06/2026
- **Status:** Em Revisão
- **Autor:** Antigravity (AI Coding Assistant)

---

## 1. Visão Geral
Com a necessidade de atualizar pedidos, o contrato gerado anteriormente para um pedido torna-se obsoleto. Este design especifica:
1. Adição de um campo `Ativo` ao modelo `ContratoAutentique` para distinguir contratos válidos.
2. Remoção do índice único do `IdPedido` na tabela `CONTRATO_AUTENTIQUE`, já que um pedido pode ter múltiplos contratos históricos (inativos) e apenas um ativo.
3. Atualização das consultas de validação para filtrar apenas por contratos ativos.
4. Enfileiramento de um novo job de contrato após a atualização de um pedido com a flag `InativarExistente` ativada, que inativará os contratos ativos anteriores antes de gerar o novo.

---

## 2. Requisitos e Alterações no Banco de Dados
- **Model `ContratoAutentique`**:
  - Nova coluna `ATIVO` do tipo `TINYINT(1)` (bool no C#), padrão `1` (true).
- **Index de `IdPedido`**:
  - Alteração do índice único `IX_CONTRATO_AUTENTIQUE_ID_PEDIDO` para não-único, permitindo múltiplos registros inativos por pedido.
- **Migration**:
  - Geração e aplicação de migração via Entity Framework Core.

---

## 3. Detalhes das Implementações

### 3.1. Model `ContratoAutentique`
Adição do campo:
```csharp
[Column("ATIVO")]
public bool Ativo { get; set; } = true;
```

### 3.2. Configuração no DbContext (`DatabaseContext.cs`)
Alteração do índice único para não-único:
```csharp
private static void ConfigureContratoAutentique(ModelBuilder modelBuilder) {
    modelBuilder.Entity<ContratoAutentique>(builder => {
        ...
        builder.Property(c => c.Ativo).HasColumnName("ATIVO").HasDefaultValue(true);
        builder.HasIndex(c => c.IdPedido); // Sem .IsUnique() para permitir históricos
        builder.HasIndex(c => c.AutentiqueDocumentId).IsUnique();
    });
}
```

### 3.3. Repositório `ContratoRepository`
- **Filtro por `Ativo` no `GetByPedidoIdAsync`**:
  ```csharp
  public async Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) {
      return await _dbContext.ContratosAutentique
          .Include(c => c.Pedido)
          .AsTracking()
          .FirstOrDefaultAsync(c => c.IdPedido == idPedido && c.Ativo);
  }
  ```
- **Novo método `InativarContratosPorPedidoIdAsync`**:
  ```csharp
  public async Task InativarContratosPorPedidoIdAsync(int idPedido) {
      var contratos = await _dbContext.ContratosAutentique
          .Where(c => c.IdPedido == idPedido && c.Ativo)
          .AsTracking()
          .ToListAsync();
      foreach (var contrato in contratos) {
          contrato.Ativo = false;
      }
      await _dbContext.SaveChangesAsync();
  }
  ```

### 3.4. Alterações na Fila (`ContratoJob` e `IContratoQueue`)
Adição da flag `InativarExistente`:
```csharp
public class ContratoJob {
    public int IdPedido { get; }
    public int Tentativas { get; set; }
    public bool InativarExistente { get; }

    public ContratoJob(int idPedido, int tentativas = 0, bool inativarExistente = false) {
        IdPedido = idPedido;
        Tentativas = tentativas;
        InativarExistente = inativarExistente;
    }
}
```

E assinaturas de enfileiramento correspondentes.

### 3.5. Background Worker (`ContratoQueueBackgroundService`)
No processamento de cada job, se a flag estiver ativada, inativa os contratos anteriores:
```csharp
private async Task ProcessarJobAsync(ContratoJob job, CancellationToken stoppingToken) {
    try {
        using var scope = _scopeFactory.CreateScope();
        
        if (job.InativarExistente) {
            var contratoRepository = scope.ServiceProvider.GetRequiredService<IContratoRepository>();
            _logger.LogInformation("Inativando contratos ativos antigos do pedido {IdPedido} devido a atualização.", job.IdPedido);
            await contratoRepository.InativarContratosPorPedidoIdAsync(job.IdPedido);
        }

        var gerarContratoUseCase = scope.ServiceProvider.GetRequiredService<GerarContratoPedido>();
        var contrato = await gerarContratoUseCase.ExecuteAsync(job.IdPedido);
        ...
```

### 3.6. Use Case `AtualizarPedido`
Injeção da fila de contratos e enfileiramento ao atualizar com sucesso:
```csharp
public class AtualizarPedido(...) {
    ...
    // Ao final de ExecuteAsync
    await _pedidoRepository.SaveAsync(pedido);
    _contratoQueue.Enfileirar(pedido.Id, inativarExistente: true);
}
```

---

## 4. Plano de Testes
1. **Teste unitário da inativação**: Verificar se chamadas com `inativarExistente: true` inativam os registros anteriores no banco.
2. **Teste do Use Case `AtualizarPedido`**: Validar que após a atualização, um job com a flag de inativação é enfileirado.
3. **Teste de banco**: Verificar que múltiplos registros de contrato com o mesmo `IdPedido` são permitidos contanto que apenas o mais recente (ou nenhum) esteja ativo.
