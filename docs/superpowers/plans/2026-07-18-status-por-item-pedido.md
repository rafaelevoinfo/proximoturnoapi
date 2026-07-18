# Status por item de pedido — devolução e renovação parciais — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que um pedido fique parcialmente devolvido e parcialmente renovado, dando status por item e mantendo `Pedido.Status` como agregado derivado.

**Architecture:** `ItemPedido` ganha `Status` (reusando o enum `StatusPedido`). Cada transição de item recalcula `Pedido.Status` por regra de precedência. `Devolver` e `Renovar` passam a operar por item; o campo `RENOVADO` é removido e o badge "renovado" passa a ser derivado de `Pedido.IdPedidoOriginal`.

**Tech Stack:** .NET 10 (ASP.NET Core), EF Core + MySQL, xUnit, Next.js/React + TypeScript.

**Spec:** `ProximoTurnoApi/docs/superpowers/specs/2026-07-18-status-por-item-pedido-design.md`

## Global Constraints

- Backend em `ProximoTurnoApi/`; frontend em `ProximoTurno/`. Comandos backend rodam a partir de `ProximoTurnoApi/`.
- Rodar testes: `dotnet test` (a partir de `ProximoTurnoApi/`). Filtrar por classe: `dotnet test --filter "FullyQualifiedName~PedidoTests"`.
- Enum de status é `ProximoTurnoApi.Domain.StatusPedido { Pendente=0, Entregue=1, Devolvido=2, Cancelado=3 }` — usado em `Pedido.Status` e (novo) em `ItemPedido.Status`.
- Padrão de notificação (Flunt) via `AddNotification("ERRO", ...)` no domínio; use cases herdam de `PedidoUseCaseBasico`.
- Não gerar migration EF antes da Task 6 (o mapeamento final do `Status` só é configurado lá).
- Mensagens/labels em português, seguindo o código existente.

---

### Task 1: Status por item + derivação de `Pedido.Status`

**Files:**
- Modify: `Src/Infrastructure/Models/ItemPedido.cs`
- Modify: `Src/Domain/Pedido.cs` (métodos `AdicionarItem`, `Entregar`, `Cancelar`; novo `RecalcularStatus`)
- Test: `Tests/Domain/PedidoTests.cs`

**Interfaces:**
- Produces: `ItemPedido.Status` (`StatusPedido`); `Pedido` mantém `AdicionarItem`/`Entregar`/`Cancelar` marcando o status de cada item; `private void Pedido.RecalcularStatus()`.

- [ ] **Step 1: Escrever os testes que falham**

Adicionar em `Tests/Domain/PedidoTests.cs` (usar os helpers privados já existentes `CriarClienteTeste()` e `CriarJogoCopiaTeste(decimal)`):

```csharp
[Fact]
public void AdicionarItem_DefineItemComoPendente()
{
    var pedido = new Pedido(CriarClienteTeste());
    var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };

    pedido.AdicionarItem(item);

    Assert.Equal(StatusPedido.Pendente, item.Status);
    Assert.Equal(StatusPedido.Pendente, pedido.Status);
}

[Fact]
public void Entregar_DefineTodosOsItensComoEntregue()
{
    var pedido = new Pedido(CriarClienteTeste());
    var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };
    pedido.AdicionarItem(item);

    pedido.Entregar(new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));

    Assert.Equal(StatusPedido.Entregue, item.Status);
    Assert.Equal(StatusPedido.Entregue, pedido.Status);
}

[Fact]
public void Cancelar_DefineTodosOsItensComoCancelado()
{
    var pedido = new Pedido(CriarClienteTeste());
    var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };
    pedido.AdicionarItem(item);

    pedido.Cancelar();

    Assert.Equal(StatusPedido.Cancelado, item.Status);
    Assert.Equal(StatusPedido.Cancelado, pedido.Status);
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests"`
Expected: FALHA de compilação/asserção (`ItemPedido` não tem `Status`).

- [ ] **Step 3: Adicionar a propriedade `Status` em `ItemPedido`**

Em `Src/Infrastructure/Models/ItemPedido.cs`, adicionar o `using` e a propriedade:

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("PEDIDO_ITEM")]
public class ItemPedido : BaseModel {
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    [Column("ID_JOGO_COPIA")]
    public int IdJogoCopia { get; set; }
    public JogoCopia JogoCopia { get; set; } = null!;

    [Column("VALOR")]
    public decimal Valor { get; set; }

    [Column("ID_PERIODO")]
    public int IdPeriodo { get; set; }

    [Column("DATA_DEVOLUCAO")]
    public DateTime DataDevolucao { get; set; }

    [Column("STATUS")]
    public StatusPedido Status { get; set; }

    [Column("RENOVADO")]
    public bool Renovado { get; set; }
}
```

(Mantém `Renovado` por enquanto — será removido na Task 6.)

- [ ] **Step 4: Marcar status do item nas transições e adicionar `RecalcularStatus`**

Em `Src/Domain/Pedido.cs`:

Em `AdicionarItem`, logo antes de `_items.Add(item);`:

```csharp
item.Status = StatusPedido.Pendente;
_items.Add(item);
```

Em `Entregar`, dentro do `foreach (var item in _items)`, adicionar:

```csharp
item.Status = StatusPedido.Entregue;
```

Em `Cancelar`, dentro do `foreach (var item in _items)`, adicionar:

```csharp
item.Status = StatusPedido.Cancelado;
```

Adicionar o método privado (perto de `RegistrarAlteracao`):

```csharp
private void RecalcularStatus() {
    if (_items.Count == 0) {
        return;
    }
    if (_items.All(i => i.Status == StatusPedido.Cancelado)) {
        Status = StatusPedido.Cancelado;
        return;
    }
    var ativos = _items.Where(i => i.Status != StatusPedido.Cancelado);
    if (ativos.Any(i => i.Status == StatusPedido.Pendente)) {
        Status = StatusPedido.Pendente;
    } else if (ativos.Any(i => i.Status == StatusPedido.Entregue)) {
        Status = StatusPedido.Entregue;
    } else {
        Status = StatusPedido.Devolvido;
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests"`
Expected: PASS (incluindo os testes já existentes de `CalcularTotal`/entrega).

- [ ] **Step 6: Commit**

```bash
git add Src/Infrastructure/Models/ItemPedido.cs Src/Domain/Pedido.cs Tests/Domain/PedidoTests.cs
git commit -m "feat: status por item de pedido e derivacao de Pedido.Status"
```

---

### Task 2: `Devolver` parcial por item

**Files:**
- Modify: `Src/Domain/Pedido.cs` (método `Devolver`)
- Test: `Tests/Domain/PedidoTests.cs`

**Interfaces:**
- Consumes: `ItemPedido.Status`, `Pedido.RecalcularStatus()` (Task 1).
- Produces: `bool Pedido.Devolver(List<int>? idsItemsDevolvidos)` agora parcial.

- [ ] **Step 1: Escrever os testes que falham**

Helper local no topo da classe `PedidoTests` (para montar pedido entregue com 2 itens):

```csharp
private Pedido PedidoEntregueComDoisItens(out ItemPedido item1, out ItemPedido item2)
{
    var pedido = new Pedido(CriarClienteTeste());
    item1 = new ItemPedido { Id = 1, JogoCopia = new JogoCopia { Id = 1, IdJogo = 10, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 10, Nome = "J1", IdCategoria = 1 } }, IdPeriodo = 1, Valor = 50m };
    item2 = new ItemPedido { Id = 2, JogoCopia = new JogoCopia { Id = 2, IdJogo = 11, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 11, Nome = "J2", IdCategoria = 1 } }, IdPeriodo = 1, Valor = 50m };
    pedido.AdicionarItem(item1);
    pedido.AdicionarItem(item2);
    pedido.Entregar(new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));
    return pedido;
}

[Fact]
public void Devolver_Parcial_MantemPedidoEntregueEItemNaoDevolvidoEntregue()
{
    var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);

    var ok = pedido.Devolver(new List<int> { item1.Id });

    Assert.True(ok);
    Assert.Equal(StatusPedido.Devolvido, item1.Status);
    Assert.Equal(StatusJogo.Disponivel, item1.JogoCopia.Status);
    Assert.Equal(StatusPedido.Entregue, item2.Status);
    Assert.Equal(StatusPedido.Entregue, pedido.Status);
}

[Fact]
public void Devolver_Todos_DeixaPedidoDevolvido()
{
    var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);

    var ok = pedido.Devolver(null);

    Assert.True(ok);
    Assert.Equal(StatusPedido.Devolvido, item1.Status);
    Assert.Equal(StatusPedido.Devolvido, item2.Status);
    Assert.Equal(StatusPedido.Devolvido, pedido.Status);
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests.Devolver"`
Expected: FALHA — hoje `Devolver` seta o pedido inteiro para `Devolvido` e não altera `ItemPedido.Status`.

- [ ] **Step 3: Reescrever `Devolver`**

Substituir o método `Devolver` em `Src/Domain/Pedido.cs` por:

```csharp
public bool Devolver(List<int>? idsItemsDevolvidos) {
    Clear();
    if (Status != StatusPedido.Entregue) {
        AddNotification("ERRO", "Não é possível devolver um pedido não entregue");
        return false;
    }
    var qtdeDevolvida = 0;
    foreach (var item in _items) {
        var deveDevolver = idsItemsDevolvidos is null || idsItemsDevolvidos.Count == 0 || idsItemsDevolvidos.Contains(item.Id);
        if (deveDevolver && item.Status == StatusPedido.Entregue) {
            item.Status = StatusPedido.Devolvido;
            item.JogoCopia.Status = StatusJogo.Disponivel;
            qtdeDevolvida++;
        }
    }
    if (qtdeDevolvida == 0) {
        AddNotification("ERRO", "Nenhum item foi devolvido");
        return false;
    }
    RecalcularStatus();
    RegistrarAlteracao();
    return true;
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Domain/Pedido.cs Tests/Domain/PedidoTests.cs
git commit -m "feat: devolucao parcial por item"
```

---

### Task 3: `Renovar` parcial + `Pedido.IdPedidoOriginal`

**Files:**
- Modify: `Src/Domain/Pedido.cs` (método `Renovar`; nova propriedade `IdPedidoOriginal`)
- Test: `Tests/Domain/PedidoTests.cs`

**Interfaces:**
- Consumes: `ItemPedido.Status`, `RecalcularStatus()` (Task 1).
- Produces: `Pedido.IdPedidoOriginal` (`int?`, `init`); `Pedido? Renovar(List<(int idItem, CategoriaPeriodoInfo periodo)?> itensRenovar, ICategoriaPeriodoCache cache)` agora parcial (não exige `Devolvido`, mantém não-renovados `Entregue`).

- [ ] **Step 1: Escrever os testes que falham**

Adicionar em `Tests/Domain/PedidoTests.cs` (reusa `PedidoEntregueComDoisItens` da Task 2):

```csharp
[Fact]
public void Renovar_Parcial_MantemPedidoAntigoEntregueEGeraNovoSoComRenovado()
{
    var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);
    var cache = new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
    var periodo = new ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo(1, 7, 50m, 1, "categoria");
    var itensRenovar = new List<(int idItem, ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo periodo)?> { (item1.Id, periodo) };

    var novo = pedido.Renovar(itensRenovar, cache);

    Assert.NotNull(novo);
    Assert.Equal(StatusPedido.Devolvido, item1.Status);
    Assert.Equal(StatusPedido.Entregue, item2.Status);
    Assert.Equal(StatusPedido.Entregue, pedido.Status);        // sobrou item fora
    Assert.Single(novo!.Items);
    Assert.Equal(StatusPedido.Entregue, novo.Status);
}

[Fact]
public void Renovar_Todos_DeixaPedidoAntigoDevolvido()
{
    var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);
    var cache = new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
    var periodo = new ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo(1, 7, 50m, 1, "categoria");
    var itensRenovar = new List<(int idItem, ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo periodo)?> { (item1.Id, periodo), (item2.Id, periodo) };

    var novo = pedido.Renovar(itensRenovar, cache);

    Assert.NotNull(novo);
    Assert.Equal(StatusPedido.Devolvido, pedido.Status);
    Assert.Equal(2, novo!.Items.Count);
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests.Renovar"`
Expected: FALHA — hoje `Renovar` exige `Status == Devolvido` e retorna `null`.

- [ ] **Step 3: Adicionar `IdPedidoOriginal` e reescrever `Renovar`**

Em `Src/Domain/Pedido.cs`, adicionar a propriedade perto de `PedidoOriginal`:

```csharp
public int? IdPedidoOriginal { get; init; }
//Em caso de renovações, estara preenchido com o pedido que gerou a renovacao
public Pedido? PedidoOriginal { get; init; }
```

Substituir o método `Renovar` por:

```csharp
public Pedido? Renovar(List<(int idItem, CategoriaPeriodoInfo periodo)?> itensRenovar, ICategoriaPeriodoCache cache) {
    Clear();
    if (Status != StatusPedido.Entregue) {
        AddNotification("ERRO", "Para renovar, o pedido precisa estar entregue");
        return null;
    }

    var novoPedido = new Pedido(Cliente) {
        DataHora = DateTime.Now,
        Status = StatusPedido.Pendente,
        DataHoraEntrega = DateTime.Now,
        PedidoOriginal = this
    };

    foreach (var item in _items) {
        var itemRenovar = itensRenovar.FirstOrDefault(i => i.HasValue && i.Value.idItem == item.Id);
        if (itemRenovar is not null && item.Status == StatusPedido.Entregue) {
            item.JogoCopia.Status = StatusJogo.Disponivel; // libera para AdicionarItem revalidar e reservar
            var novoItem = new ItemPedido() {
                IdJogoCopia = item.IdJogoCopia,
                JogoCopia = item.JogoCopia,
                IdPeriodo = itemRenovar.Value.periodo.IdPeriodo,
                Valor = itemRenovar.Value.periodo.Valor,
                DataDevolucao = novoPedido.CalcularDataDevolucao(itemRenovar.Value.periodo.QuantidadeDias)
            };
            novoPedido.AdicionarItem(novoItem);
            item.Status = StatusPedido.Devolvido; // fecha a perna antiga do item renovado
        }
    }

    if (novoPedido.Items.Count == 0) {
        AddNotification("ERRO", "Nenhum item válido para renovação");
        return null;
    }

    novoPedido.CalcularTotal();
    novoPedido.Entregar(cache);

    RecalcularStatus();
    RegistrarAlteracao();
    return novoPedido;
}
```

(Removido o `Renovado = true` na criação do `novoItem` — o badge passa a ser derivado do `PedidoOriginal`. Removido o pré-requisito `Status == Devolvido`.)

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~PedidoTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Domain/Pedido.cs Tests/Domain/PedidoTests.cs
git commit -m "feat: renovacao parcial mantendo itens nao-renovados entregues"
```

---

### Task 4: Reescrever o use case `RenovarPedido`

**Files:**
- Modify: `Src/Application/UseCases/Pedido/RenovarPedido.cs`
- Test: `Tests/Domain/RenovarPedidoTests.cs`

**Interfaces:**
- Consumes: `Pedido.Renovar(...)` parcial (Task 3).

- [ ] **Step 1: Escrever/atualizar os testes que falham**

Em `Tests/Domain/RenovarPedidoTests.cs`, trocar o helper `PedidoEntregue` para permitir dois itens e adicionar um caso parcial. Substituir o helper existente e adicionar o teste:

```csharp
private static Pedido PedidoEntregueComDoisItens(FakeCategoriaPeriodoCache cache, int idPedido = 1) {
    var pedido = new Pedido(PedidoTestFactory.Cliente()) { Id = idPedido };
    pedido.AdicionarItem(new ItemPedido { Id = 1, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1) });
    pedido.AdicionarItem(new ItemPedido { Id = 2, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 2, idJogo: 6, idCategoria: 1) });
    pedido.Entregar(cache);
    return pedido;
}

[Fact]
public async Task ExecuteAsync_RenovacaoParcial_MantemPedidoOriginalEntregue() {
    var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
    var (useCase, repo, queue) = Criar(cache);
    repo.Pedidos.Add(PedidoEntregueComDoisItens(cache, 1));

    await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = null }]);

    Assert.True(useCase.IsValid);
    var original = repo.Pedidos.First(p => p.Id == 1);
    Assert.Equal(StatusPedido.Entregue, original.Status);              // sobrou o item 2
    Assert.Contains(repo.Pedidos, p => p.PedidoOriginal != null && p.Items.Count == 1 && p.Status == StatusPedido.Entregue);
    Assert.Single(queue.Enfileirados);
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~RenovarPedidoTests"`
Expected: FALHA — o use case atual devolve os não-renovados e força o pedido a `Devolvido`.

- [ ] **Step 3: Remover o passo de devolução do use case**

Em `Src/Application/UseCases/Pedido/RenovarPedido.cs`, remover o bloco que calcula `idsItensDevolucao` e chama `pedidoExistente.Devolver(...)` (as linhas entre a checagem de `itensNovoPedido.Count == 0` e a chamada de `Renovar`). O trecho final deve ficar:

```csharp
        if (itensNovoPedido.Count == 0) {
            logger.LogWarning("Nenhum item válido foi processado para renovação do pedido {PedidoId}.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum item foi informado para ser renovado"));
            return;
        }

        var novoPedido = pedidoExistente.Renovar(itensNovoPedido, _categoriaPeriodoCache);
        if (novoPedido is null || !pedidoExistente.IsValid || !novoPedido.IsValid) {
            var errors = string.Join(", ", pedidoExistente.Notifications.Concat(novoPedido?.Notifications ?? []).Select(n => n.Message));
            logger.LogWarning("Regra de negócio impediu renovação do pedido {PedidoId}: {Errors}", idPedido, errors);
            var notifications = pedidoExistente.Notifications
                .Concat(novoPedido?.Notifications ?? [])
                .Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message))
                .ToList();
            AddNotifications((IList<UseCaseNotification>)notifications);
            return;
        }

        try {
            await _pedidoRepository.SaveAsync(pedidoExistente, false);
            await _pedidoRepository.SaveAsync(novoPedido);
            logger.LogInformation("Renovação do pedido {PedidoId} concluída. Novo pedido gerado: {NovoPedidoId}.", idPedido, novoPedido.Id);
            _contratoQueue.Enfileirar(novoPedido.Id);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar renovação do pedido {PedidoId}.", idPedido);
            throw;
        }
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~RenovarPedidoTests"`
Expected: PASS (o teste existente `ExecuteAsync_QuandoValido...` continua válido: renovando o único item, o original vira `Devolvido` e o novo `Entregue`).

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Pedido/RenovarPedido.cs Tests/Domain/RenovarPedidoTests.cs
git commit -m "feat: use case de renovacao parcial (sem devolver os nao-renovados)"
```

---

### Task 5: DTO — `Status` no item, `Renovado` e `Atrasado` derivados

**Files:**
- Modify: `Src/Application/DTOs/PedidoDTO.cs`
- Test: `Tests/Domain/PedidoDTOTests.cs` (criar)

**Interfaces:**
- Consumes: `ItemPedido.Status`, `Pedido.IdPedidoOriginal` (Tasks 1, 3).
- Produces: `ItemPedidoDTO.Status` (`StatusPedido`); `ItemPedidoDTO.Renovado` derivado; `PedidoDTO.Atrasado` por item.

- [ ] **Step 1: Escrever o teste que falha**

Criar `Tests/Domain/PedidoDTOTests.cs`:

```csharp
using System.Collections.Generic;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoDTOTests {
    [Fact]
    public void FromModel_ExpoeStatusDoItem_EDerivaRenovado() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
        var pedido = new Pedido(PedidoTestFactory.Cliente()) { Id = 1 };
        pedido.AdicionarItem(new ItemPedido { Id = 1, IdPeriodo = 1, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(1, 10, 1) });
        pedido.Entregar(cache);

        var dto = PedidoDTO.FromModel(pedido);

        Assert.Equal(StatusPedido.Entregue, dto.Items![0].Status);
        Assert.False(dto.Items[0].Renovado);      // pedido sem PedidoOriginal
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha**

Run: `dotnet test --filter "FullyQualifiedName~PedidoDTOTests"`
Expected: FALHA de compilação (`ItemPedidoDTO` não tem `Status`).

- [ ] **Step 3: Ajustar o DTO**

Em `Src/Application/DTOs/PedidoDTO.cs`:

Em `ItemPedidoDTO`, adicionar a propriedade `Status`:

```csharp
public record ItemPedidoDTO {
    public int Id { get; set; }

    public JogoResumoDTO? Jogo { get; set; } = null!;

    public int IdPeriodo { get; set; }

    public decimal Valor { get; set; }
    public DateTime DataDevolucao { get; set; }

    public StatusPedido Status { get; set; }

    public bool Renovado { get; set; }
}
```

Em `PedidoDTO.FromModel`, ajustar `Atrasado` e o mapeamento dos itens:

```csharp
            Status = pedido.Status,
            Atrasado = pedido.Items.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < DateTime.Today),
            MetodoPagamento = pedido.MetodoPagamento,
            MetodoEntrega = pedido.MetodoEntrega,
            DataHoraAlteracao = pedido.DataHoraAlteracao,
            Items = pedido.Items.Select(i => new ItemPedidoDTO {
                Id = i.Id,
                Jogo = JogoResumoDTO.FromModel(i.JogoCopia.Jogo!),
                IdPeriodo = i.IdPeriodo,
                Valor = i.Valor,
                DataDevolucao = i.DataDevolucao,
                Status = i.Status,
                Renovado = pedido.IdPedidoOriginal != null
            }).ToList()
```

- [ ] **Step 4: Rodar o teste e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~PedidoDTOTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/DTOs/PedidoDTO.cs Tests/Domain/PedidoDTOTests.cs
git commit -m "feat: DTO expoe status por item e deriva renovado/atrasado"
```

---

### Task 6: EF mapping + migration + remover `ItemPedido.Renovado`

**Files:**
- Modify: `Src/Infrastructure/Models/ItemPedido.cs` (remover `Renovado`)
- Modify: `Src/Infrastructure/Repositories/DatabaseContext.cs` (conversão de `ItemPedido.Status`; FK `IdPedidoOriginal`)
- Create: `Src/Migrations/<timestamp>_AddStatusItemPedidoRemoveRenovado.cs` (gerado)

**Interfaces:**
- Consumes: `ItemPedido.Status`, `Pedido.IdPedidoOriginal` (Tasks 1, 3).

Pré-condição: neste ponto não há mais referências a `ItemPedido.Renovado` no código (Task 3 removeu no domínio, Task 5 no DTO).

- [ ] **Step 1: Remover a propriedade `Renovado` do modelo**

Em `Src/Infrastructure/Models/ItemPedido.cs`, remover:

```csharp
    [Column("RENOVADO")]
    public bool Renovado { get; set; }
```

- [ ] **Step 2: Configurar mapeamento no `DatabaseContext`**

Em `Src/Infrastructure/Repositories/DatabaseContext.cs`:

Trocar a shadow FK do `PedidoOriginal` para a propriedade CLR (dentro de `ConfigurePedido`):

```csharp
            builder.Property(p => p.IdPedidoOriginal).HasColumnName("ID_PEDIDO_ORIGINAL").IsRequired(false);

            builder.HasOne(p => p.PedidoOriginal)
                   .WithMany()
                   .HasForeignKey(p => p.IdPedidoOriginal)
                   .OnDelete(DeleteBehavior.Restrict);
```

Configurar a conversão do `ItemPedido.Status` (junto do bloco `modelBuilder.Entity<ItemPedido>()`):

```csharp
        modelBuilder.Entity<ItemPedido>()
            .HasOne(pj => pj.JogoCopia)
            .WithMany()
            .HasForeignKey(pj => pj.IdJogoCopia)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ItemPedido>()
            .Property(i => i.Status).HasConversion<short>();
```

- [ ] **Step 3: Compilar para garantir que não há referências pendentes a `Renovado`**

Run: `dotnet build Src/ProximoTurnoApi.csproj`
Expected: BUILD OK (sem erros `CS...Renovado`).

- [ ] **Step 4: Gerar a migration**

Run: `dotnet ef migrations add AddStatusItemPedidoRemoveRenovado --project Src/ProximoTurnoApi.csproj`
Expected: cria arquivo em `Src/Migrations/`. Conferir que o `Up()` contém `AddColumn STATUS` em `PEDIDO_ITEM` e `DropColumn RENOVADO`, e que **não** há alteração de schema para `ID_PEDIDO_ORIGINAL` (mesma coluna).

- [ ] **Step 5: Adicionar o backfill na migration**

No `Up()` do arquivo gerado, garantir o `defaultValue` da coluna e inserir o backfill **entre** o `AddColumn` do `STATUS` e o `DropColumn` do `RENOVADO`:

```csharp
migrationBuilder.AddColumn<short>(
    name: "STATUS",
    table: "PEDIDO_ITEM",
    type: "smallint",
    nullable: false,
    defaultValue: (short)0);

migrationBuilder.Sql("UPDATE PEDIDO_ITEM pi JOIN PEDIDO p ON pi.ID_PEDIDO = p.ID SET pi.STATUS = p.STATUS;");

migrationBuilder.DropColumn(
    name: "RENOVADO",
    table: "PEDIDO_ITEM");
```

(Ajustar o `type`/parâmetros conforme o que o EF gerou para o provider MySQL; manter apenas a inserção da linha `migrationBuilder.Sql(...)` de backfill.)

- [ ] **Step 6: Aplicar a migration e rodar a suíte**

Run: `dotnet ef database update --project Src/ProximoTurnoApi.csproj`
Run: `dotnet test`
Expected: migration aplica sem erro; todos os testes PASS.

- [ ] **Step 7: Commit**

```bash
git add Src/Infrastructure/Models/ItemPedido.cs Src/Infrastructure/Repositories/DatabaseContext.cs Src/Migrations/
git commit -m "feat: migration status por item + backfill e remocao de RENOVADO"
```

---

### Task 7: Filtro "Atrasados" por item

**Files:**
- Modify: `Src/Infrastructure/Repositories/PedidoRepository.cs:46`
- Test: `Tests/Domain/BuscarPedidosTests.cs` (adicionar caso, se o repo real não for exercitado por teste, cobrir via `PedidoDTOTests` — ver nota)

**Interfaces:**
- Consumes: `ItemPedido.Status` mapeado (Task 6).

- [ ] **Step 1: Ajustar o filtro**

Em `Src/Infrastructure/Repositories/PedidoRepository.cs`, no bloco `if (filtro.Atrasados)`:

```csharp
        if (filtro.Atrasados) {
            // nao vou considerar horas, apenas dias
            query = query.Where(p => p.Items!.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < DateTime.Today));
        }
```

- [ ] **Step 2: Verificar build e testes**

Run: `dotnet test`
Expected: PASS. (O `PedidoRepository` acessa MySQL, não é coberto por teste unitário; a paridade do critério "atrasado" já está coberta em `PedidoDTOTests` via `Atrasado` por item da Task 5. A alteração aqui é a mesma expressão traduzida para a query.)

- [ ] **Step 3: Commit**

```bash
git add Src/Infrastructure/Repositories/PedidoRepository.cs
git commit -m "feat: filtro de atrasados por item entregue"
```

---

### Task 8: Elegibilidade de comentário por item devolvido

**Files:**
- Modify: `Src/Application/UseCases/Comentario/PodeComentarJogo.cs:28-31`
- Modify: `Src/Application/UseCases/Comentario/SalvarComentario.cs:27-52`

**Interfaces:**
- Consumes: `ItemPedido.Status` mapeado (Task 6).

Decisão (b): qualquer item `Devolvido` daquele jogo libera comentário (renovado inclusive), independente do status do pedido.

- [ ] **Step 1: Ajustar `PodeComentarJogo`**

Em `Src/Application/UseCases/Comentario/PodeComentarJogo.cs`, trocar a query `jaDevolveu`:

```csharp
        var jaDevolveu = await dbContext.Pedidos
            .AnyAsync(p => p.Cliente.Id == idCliente.Value &&
                           p.Items.Any(i => i.JogoCopia.IdJogo == jogoId && i.Status == StatusPedido.Devolvido));
```

- [ ] **Step 2: Ajustar `SalvarComentario`**

Em `Src/Application/UseCases/Comentario/SalvarComentario.cs`, trocar a projeção e o cálculo de `temPedidoDevolvido`:

```csharp
        var pedidosComJogo = await dbContext.Pedidos
            .Where(p => p.Cliente.Id == idCliente.Value && p.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo))
            .AsNoTracking()
            .Select(p =>
                new {
                    p.Id,
                    TemItemDevolvido = p.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo && i.Status == StatusPedido.Devolvido)
                }
            )
            .ToListAsync();

        if (!pedidosComJogo.Any()) {
            logger.LogWarning("Jogo {JogoId} não foi alugado pelo cliente {ClienteId}.", dto.IdJogo, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Você só pode comentar em jogos que já alugou."));
            return null;
        }

        var temPedidoDevolvido = pedidosComJogo.Any(p => p.TemItemDevolvido);
```

(As linhas 48-52, que emitem a notificação "Comentários são permitidos apenas para jogos já devolvidos" quando `!temPedidoDevolvido`, permanecem iguais.)

- [ ] **Step 3: Verificar build e testes**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Src/Application/UseCases/Comentario/PodeComentarJogo.cs Src/Application/UseCases/Comentario/SalvarComentario.cs
git commit -m "feat: comentario liberado por item devolvido"
```

---

### Task 9: Frontend — tipo `status` no item e badges

**Files:**
- Modify: `ProximoTurno/lib/api-service.ts:93-104` (interface `ItemPedido`)
- Modify: `ProximoTurno/components/pedidos/pedido-detalhes-dialog.tsx:169-181`

**Interfaces:**
- Consumes: `ItemPedidoDTO.Status` no JSON da API (Task 5).

- [ ] **Step 1: Adicionar `status` ao tipo `ItemPedido`**

Em `ProximoTurno/lib/api-service.ts`, na interface `ItemPedido`:

```typescript
export interface ItemPedido {
    id: number
    jogo: {
        id: number
        nome: string
        idCategoria?: number
    }
    idPeriodo: number
    valor: number
    dataDevolucao: string
    status: number
    renovado: boolean
}
```

- [ ] **Step 2: Mostrar badge de status por item no dialog**

Em `ProximoTurno/components/pedidos/pedido-detalhes-dialog.tsx`, na célula do jogo (onde já existe o badge "renovado"), adicionar o badge de status por item:

```tsx
                                                    <td className="py-2">
                                                        {item.jogo.nome}
                                                        {item.renovado && (
                                                            <Badge variant="secondary" className="ml-2 text-[10px] px-1.5 py-0">renovado</Badge>
                                                        )}
                                                        {item.status === 2 && (
                                                            <Badge variant="outline" className="ml-2 text-[10px] px-1.5 py-0">devolvido</Badge>
                                                        )}
                                                        {item.status === 1 && (
                                                            <Badge variant="default" className="ml-2 text-[10px] px-1.5 py-0">entregue</Badge>
                                                        )}
                                                    </td>
```

(`1 = Entregue`, `2 = Devolvido`, seguindo o enum `StatusPedido`.)

- [ ] **Step 3: Verificar build do frontend**

Run (a partir de `ProximoTurno/`): `npm run build`
Expected: build sem erros de tipo. Verificar manualmente no dialog de um pedido parcialmente devolvido/renovado que os badges aparecem por item.

- [ ] **Step 4: Commit**

```bash
git add ProximoTurno/lib/api-service.ts ProximoTurno/components/pedidos/pedido-detalhes-dialog.tsx
git commit -m "feat: badges de status por item no dialog de detalhes"
```

---

## Notas de verificação final (após todas as tasks)

- `dotnet test` (a partir de `ProximoTurnoApi/`) — suíte inteira verde.
- `dotnet build Src/ProximoTurnoApi.csproj` — sem warnings de referências a `Renovado`.
- Fluxo manual (API + front): entregar um pedido com 2 itens → renovar só 1 → pedido original permanece `Entregue` com 1 item `Devolvido` e 1 `Entregue`; novo pedido `Entregue` só com o renovado; badge "renovado" aparece no novo pedido.
