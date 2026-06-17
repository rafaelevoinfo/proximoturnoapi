# Plano de Implementação: Campo Ativo e Inativação de Contratos

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar o campo `Ativo` (default true) no modelo de contrato, atualizar as validações para considerar apenas contratos ativos, remover a restrição de unicidade por pedido e disparar a inativação de contratos anteriores ao atualizar um pedido.

**Architecture:** Modificação no banco de dados através de migração do EF Core. Atualização de repositórios, da estrutura da mensagem da fila, do background worker e do Use Case de atualização.

**Tech Stack:** .NET 10.0, Entity Framework Core, MySql

---

### Task 1: Ajustar Modelo, DbContext e Gerar Migration

**Files:**
- Modify: `Src/Infrastructure/Models/ContratoAutentique.cs`
- Modify: `Src/Infrastructure/Repositories/DatabaseContext.cs`
- Create: Migração EF Core (arquivos gerados pelo comando `ef migrations`)

- [ ] **Step 1: Modificar `ContratoAutentique.cs`**
  Adicionar a propriedade `Ativo` com a anotação `Column("ATIVO")` (linha 36):
  ```csharp
      [Column("ATIVO")]
      public bool Ativo { get; set; } = true;
  ```

- [ ] **Step 2: Modificar `DatabaseContext.cs`**
  Alterar a configuração de mapeamento de `ConfigureContratoAutentique` para remover `.IsUnique()` do `IdPedido` e mapear `Ativo`:
  
  Código antigo (linhas 213-228):
  ```csharp
      private static void ConfigureContratoAutentique(ModelBuilder modelBuilder) {
          modelBuilder.Entity<ContratoAutentique>(builder => {
              builder.ToTable("CONTRATO_AUTENTIQUE");
              builder.HasKey(c => c.Id);
              builder.Property(c => c.Id).HasColumnName("ID");
              builder.Property(c => c.Status).HasColumnName("STATUS").HasConversion<short>();
  
              builder.HasOne(c => c.Pedido)
                     .WithMany()
                     .HasForeignKey(c => c.IdPedido)
                     .OnDelete(DeleteBehavior.Restrict);
  
              builder.HasIndex(c => c.IdPedido).IsUnique();
              builder.HasIndex(c => c.AutentiqueDocumentId).IsUnique();
          });
      }
  ```

  Código novo (linhas 213-229):
  ```csharp
      private static void ConfigureContratoAutentique(ModelBuilder modelBuilder) {
          modelBuilder.Entity<ContratoAutentique>(builder => {
              builder.ToTable("CONTRATO_AUTENTIQUE");
              builder.HasKey(c => c.Id);
              builder.Property(c => c.Id).HasColumnName("ID");
              builder.Property(c => c.Status).HasColumnName("STATUS").HasConversion<short>();
              builder.Property(c => c.Ativo).HasColumnName("ATIVO").HasDefaultValue(true);
  
              builder.HasOne(c => c.Pedido)
                     .WithMany()
                     .HasForeignKey(c => c.IdPedido)
                     .OnDelete(DeleteBehavior.Restrict);
  
              builder.HasIndex(c => c.IdPedido); // Sem .IsUnique() para permitir históricos
              builder.HasIndex(c => c.AutentiqueDocumentId).IsUnique();
          });
      }
  ```

- [ ] **Step 3: Criar a migração do EF Core**
  Executar: `dotnet ef migrations add AddContratoAtivoField --project Src/ProximoTurnoApi.csproj`
  Esperado: Sucesso na geração dos arquivos de migração sob a pasta `Migrations/`.

- [ ] **Step 4: Aplicar a migração no banco de dados**
  Executar: `dotnet ef database update --project Src/ProximoTurnoApi.csproj`
  Esperado: Sucesso na aplicação da nova coluna e alteração do índice.

---

### Task 2: Atualizar Fila (`ContratoJob` e `IContratoQueue` / `ContratoQueue`)

**Files:**
- Modify: `Src/Application/UseCases/Contrato/ContratoJob.cs`
- Modify: `Src/Application/UseCases/Contrato/IContratoQueue.cs`
- Modify: `Src/Application/UseCases/Contrato/ContratoQueue.cs`
- Modify: `Tests/Domain/ContratoQueueTests.cs`

- [ ] **Step 1: Escrever teste unitário para o novo parâmetro da fila**
  Atualizar o teste em `Tests/Domain/ContratoQueueTests.cs` para passar a flag `inativarExistente` e certificar-se de que é preservada:
  ```csharp
      [Fact]
      public async Task Enfileirar_ComFlagInativarExistente_DevePreservarFlagNoJob()
      {
          // Arrange
          var queue = new ContratoQueue();

          // Act
          queue.Enfileirar(42, 0, inativarExistente: true);
          var job = await queue.DesenfileirarAsync(CancellationToken.None);

          // Assert
          Assert.Equal(42, job.IdPedido);
          Assert.True(job.InativarExistente);
      }
  ```

- [ ] **Step 2: Executar o teste e verificar se falha**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueTests"`
  Esperado: Falha de compilação (construtor de `ContratoJob` ou método `Enfileirar` não aceitam `inativarExistente`).

- [ ] **Step 3: Atualizar `ContratoJob.cs`**
  Modificar a classe para armazenar `InativarExistente` (bool):
  ```csharp
  namespace ProximoTurnoApi.Application.UseCases;

  public class ContratoJob
  {
      public int IdPedido { get; }
      public int Tentativas { get; set; }
      public bool InativarExistente { get; }

      public ContratoJob(int idPedido, int tentativas = 0, bool inativarExistente = false)
      {
          IdPedido = idPedido;
          Tentativas = tentativas;
          InativarExistente = inativarExistente;
      }
  }
  ```

- [ ] **Step 4: Atualizar `IContratoQueue.cs`**
  ```csharp
  using System.Threading;
  using System.Threading.Tasks;

  namespace ProximoTurnoApi.Application.UseCases;

  public interface IContratoQueue
  {
      void Enfileirar(int idPedido, int tentativa = 0, bool inativarExistente = false);
      ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 5: Atualizar `ContratoQueue.cs`**
  ```csharp
      public void Enfileirar(int idPedido, int tentativa = 0, bool inativarExistente = false)
      {
          _channel.Writer.TryWrite(new ContratoJob(idPedido, tentativa, inativarExistente));
      }
  ```

- [ ] **Step 6: Executar testes da fila**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueTests"`
  Esperado: PASS.

---

### Task 3: Atualizar `IContratoRepository` e `ContratoRepository`

**Files:**
- Modify: `Src/Infrastructure/Repositories/ContratoRepository.cs`

- [ ] **Step 1: Modificar `IContratoRepository.cs` e `ContratoRepository.cs`**
  Adicionar a assinatura do novo método `InativarContratosPorPedidoIdAsync` na interface e atualizar `GetByPedidoIdAsync` para filtrar por `Ativo`:
  
  Assinatura em `IContratoRepository` (linha 10):
  ```csharp
      Task InativarContratosPorPedidoIdAsync(int idPedido);
  ```

  Implementações em `ContratoRepository.cs`:
  
  Filtrar por ativo:
  ```csharp
      public async Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) {
          return await _dbContext.ContratosAutentique
              .Include(c => c.Pedido)
              .AsTracking()
              .FirstOrDefaultAsync(c => c.IdPedido == idPedido && c.Ativo);
      }
  ```

  Inativação:
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

- [ ] **Step 2: Executar `dotnet build`**
  Esperado: Sucesso na compilação.

---

### Task 4: Atualizar `ContratoQueueBackgroundService` e Testes de Integração

**Files:**
- Modify: `Src/Application/UseCases/Contrato/ContratoQueueBackgroundService.cs`
- Modify: `Tests/Domain/ContratoQueueBackgroundServiceTests.cs`

- [ ] **Step 1: Escrever teste para a lógica de inativação**
  Adicionar um teste em `Tests/Domain/ContratoQueueBackgroundServiceTests.cs` que valide a chamada para `InativarContratosPorPedidoIdAsync` quando `job.InativarExistente` for verdadeiro.
  
  Definir interface dummy/fake para o repositório ou mock:
  ```csharp
      private class FakeContratoRepository : IContratoRepository
      {
          public int InativarChamadoParaPedidoId { get; private set; }
          public Task InativarContratosPorPedidoIdAsync(int idPedido)
          {
              InativarChamadoParaPedidoId = idPedido;
              return Task.CompletedTask;
          }
          public Task SaveAsync(ContratoAutentique contrato, bool commit = true) => Task.CompletedTask;
          public Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) => Task.FromResult<ContratoAutentique?>(null);
          public Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string id) => Task.FromResult<ContratoAutentique?>(null);
          public Task SaveChangesAsync() => Task.CompletedTask;
          public Task StartTransactionAsync() => Task.CompletedTask;
          public Task CommitTransactionAsync() => Task.CompletedTask;
          public Task RollbackTransactionAsync() => Task.CompletedTask;
      }
  ```

  E o teste:
  ```csharp
      [Fact]
      public async Task ExecuteAsync_QuandoInativarExistenteEhTrue_DeveChamarInativacaoNoRepositorio()
      {
          // Arrange
          var queue = new FakeContratoQueue();
          queue.Enfileirar(42, 0, inativarExistente: true);

          var repo = new FakeContratoRepository();
          // Retorna o fake repo ou lança exceção para interromper após a inativação
          var serviceProvider = new FakeServiceProvider(t =>
          {
              if (t == typeof(IContratoRepository)) return repo;
              if (t == typeof(GerarContratoPedido)) throw new OperationCanceledException(); // Para interromper após a inativação
              return null;
          });
          var scopeFactory = new FakeServiceScopeFactory(serviceProvider);
          
          var service = new ContratoQueueBackgroundService(
              queue, 
              scopeFactory, 
              NullLogger<ContratoQueueBackgroundService>.Instance,
              new[] { TimeSpan.FromMilliseconds(1) }
          );

          // Act
          using var cts = new CancellationTokenSource();
          var runTask = service.StartAsync(cts.Token);
          await Task.Delay(100);
          cts.Cancel();

          // Assert
          Assert.Equal(42, repo.InativarChamadoParaPedidoId);
      }
  ```

- [ ] **Step 2: Executar o teste e certificar-se de que falha**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueBackgroundServiceTests"`
  Esperado: Compilação falha ou teste falha (porque a inativação não está implementada no worker).

- [ ] **Step 3: Modificar `ContratoQueueBackgroundService.cs`**
  No método `ProcessarJobAsync` (linha 62+), obter `IContratoRepository` e chamar `InativarContratosPorPedidoIdAsync` caso `job.InativarExistente` seja verdadeiro:
  ```csharp
      private async Task ProcessarJobAsync(ContratoJob job, CancellationToken stoppingToken)
      {
          try
          {
              using var scope = _scopeFactory.CreateScope();
              
              if (job.InativarExistente)
              {
                  var contratoRepository = scope.ServiceProvider.GetRequiredService<IContratoRepository>();
                  _logger.LogInformation("Inativando contratos ativos antigos do pedido {IdPedido} devido a atualização.", job.IdPedido);
                  await contratoRepository.InativarContratosPorPedidoIdAsync(job.IdPedido);
              }

              var gerarContratoUseCase = scope.ServiceProvider.GetRequiredService<GerarContratoPedido>();
              var contrato = await gerarContratoUseCase.ExecuteAsync(job.IdPedido);
              ...
  ```

- [ ] **Step 4: Executar testes de integração**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueBackgroundServiceTests"`
  Esperado: PASS.

---

### Task 5: Integrar no Use Case `AtualizarPedido`

**Files:**
- Modify: `Src/Application/UseCases/Pedido/AtualizarPedido.cs`

- [ ] **Step 1: Modificar `AtualizarPedido.cs` para injetar `IContratoQueue` e enfileirar**
  Alterar a declaração da classe `AtualizarPedido` e enfileirar o contrato com `inativarExistente: true` logo após a gravação bem-sucedida:
  
  Código antigo (linhas 7-11):
  ```csharp
  public class AtualizarPedido(IPedidoRepository pedidoRepository,
      IJogoRepository _jogoRepository,
      ICategoriaRepository _categoriaRepository,
      ValidarCupom _validarCupom,
      ILogger<AtualizarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {
  ```

  Código novo (linhas 7-12):
  ```csharp
  public class AtualizarPedido(IPedidoRepository pedidoRepository,
      IJogoRepository _jogoRepository,
      ICategoriaRepository _categoriaRepository,
      ValidarCupom _validarCupom,
      IContratoQueue _contratoQueue,
      ILogger<AtualizarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {
  ```

  Código antigo (linhas 108-111):
  ```csharp
          try {
              await _pedidoRepository.SaveAsync(pedido);
              logger.LogInformation("Pedido {PedidoId} atualizado com sucesso.", pedido.Id);
          } catch (Exception ex) {
  ```

  Código novo (linhas 108-114):
  ```csharp
          try {
              await _pedidoRepository.SaveAsync(pedido);
              logger.LogInformation("Pedido {PedidoId} atualizado com sucesso.", pedido.Id);
              
              // Enfileira a nova geração de contrato inativando os anteriores
              _contratoQueue.Enfileirar(pedido.Id, inativarExistente: true);
          } catch (Exception ex) {
  ```

- [ ] **Step 2: Registrar a nova dependência de `AtualizarPedido`**
  (O registro já é tratado automaticamente via injeção de dependência pelo contêiner do ASP.NET Core, pois registramos `IContratoQueue` as Singleton no `Program.cs` na tarefa anterior).

- [ ] **Step 3: Executar a suíte completa de testes**
  Executar: `dotnet test`
  Esperado: Sucesso em todos os testes do projeto.
