# Data de devolução na entrega do pedido — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir informar (opcionalmente) uma data de devolução ao entregar um pedido, calculando-a automaticamente a partir da data de entrega quando não informada, e corrigir o bug em que a data de devolução não é recalculada na entrega.

**Architecture:** Um cache singleton em memória (`ICategoriaPeriodoCache`) mantém `IdPeriodo → (QuantidadeDias, Valor, IdCategoria, Descrição)`, carregado no startup e invalidado na camada de infraestrutura sempre que categorias/períodos mudam. O domínio `Pedido.Entregar(cache, dataDevolucao?)` recalcula a data de devolução na entrega. O cache também passa a servir `ValidarAdicionarItem` e `RenovarPedido`, eliminando hits redundantes no banco.

**Tech Stack:** .NET 10, EF Core (MySQL), Flunt (notification pattern), xUnit; Next.js 16 / React 19 (frontend).

## Global Constraints

- Backend: `Src/` (Clean Architecture: Application / Domain / Infrastructure). Use cases herdam de `UseCaseBasico`, validação via Flunt. Repositórios registrados em `Program.cs`.
- O domínio já referencia `ProximoTurnoApi.Infrastructure.Models`; referenciar `Infrastructure.Services` do domínio é aceitável neste código.
- Regra de validação da data informada: `dataDevolucao.Value.Date > DateTime.Now.Date` (estritamente futura; hoje/passado rejeitados).
- Normalização de data de devolução para fim do dia: `.Date.AddHours(23).AddMinutes(59).AddSeconds(59)` (padrão de `CalcularDataDevolucao`).
- Fallback quando `IdPeriodo` ausente no cache: `QuantidadeDias = 1` + `LogWarning`.
- O cache carrega ativos **e** inativos (`FiltroCategoriaDTO { ApenasAtivos = false }`).
- Sem migração de schema.
- Testes: xUnit puro (sem lib de mock) — usar fakes/implementações in-test.
- Comandos: build `dotnet build ProximoTurnoApi.slnx`; testes `dotnet test ProximoTurnoApi.slnx` (a partir de `ProximoTurnoApi/`).

---

### Task 1: Cache de períodos (`ICategoriaPeriodoCache`)

**Files:**
- Create: `Src/Infrastructure/Services/CategoriaPeriodoCache.cs`
- Test: `Tests/Infrastructure/CategoriaPeriodoCacheTests.cs`

**Interfaces:**
- Produces:
  - `record CategoriaPeriodoInfo(int IdPeriodo, int QuantidadeDias, decimal Valor, int IdCategoria, string DescricaoCategoria)`
  - `interface ICategoriaPeriodoCache { bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info); int GetQuantidadeDias(int idPeriodo, int defaultDias = 1); Task RefreshAsync(); }`
  - `class CategoriaPeriodoCache` com método público `void AtualizarCache(IEnumerable<CategoriaPeriodoInfo> periodos)` (seam testável).

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class CategoriaPeriodoCacheTests {
    private static CategoriaPeriodoCache CriarCache() =>
        new(scopeFactory: null!, logger: NullLogger<CategoriaPeriodoCache>.Instance);

    [Fact]
    public void GetQuantidadeDias_QuandoPeriodoExiste_RetornaValorDoCache() {
        var cache = CriarCache();
        cache.AtualizarCache([new CategoriaPeriodoInfo(5, 7, 30m, 1, "Standard")]);

        Assert.Equal(7, cache.GetQuantidadeDias(5));
    }

    [Fact]
    public void GetQuantidadeDias_QuandoPeriodoAusente_RetornaDefault() {
        var cache = CriarCache();
        cache.AtualizarCache([]);

        Assert.Equal(1, cache.GetQuantidadeDias(999));
    }

    [Fact]
    public void TryGetPeriodo_QuandoExiste_RetornaTrueEInfo() {
        var cache = CriarCache();
        cache.AtualizarCache([new CategoriaPeriodoInfo(5, 7, 30m, 2, "Premium")]);

        Assert.True(cache.TryGetPeriodo(5, out var info));
        Assert.Equal(2, info!.IdCategoria);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (from `ProximoTurnoApi/`): `dotnet test ProximoTurnoApi.slnx --filter FullyQualifiedName~CategoriaPeriodoCacheTests`
Expected: FAIL de compilação (`CategoriaPeriodoCache` não existe).

- [ ] **Step 3: Create the cache implementation**

```csharp
using Microsoft.Extensions.DependencyInjection;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Infrastructure.Services;

public record CategoriaPeriodoInfo(
    int IdPeriodo,
    int QuantidadeDias,
    decimal Valor,
    int IdCategoria,
    string DescricaoCategoria);

public interface ICategoriaPeriodoCache {
    bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info);
    int GetQuantidadeDias(int idPeriodo, int defaultDias = 1);
    Task RefreshAsync();
}

public class CategoriaPeriodoCache(
    IServiceScopeFactory scopeFactory,
    ILogger<CategoriaPeriodoCache> logger) : ICategoriaPeriodoCache {

    private volatile IReadOnlyDictionary<int, CategoriaPeriodoInfo> _porPeriodo =
        new Dictionary<int, CategoriaPeriodoInfo>();

    public void AtualizarCache(IEnumerable<CategoriaPeriodoInfo> periodos) {
        _porPeriodo = periodos.ToDictionary(p => p.IdPeriodo);
    }

    public bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info) =>
        _porPeriodo.TryGetValue(idPeriodo, out info);

    public int GetQuantidadeDias(int idPeriodo, int defaultDias = 1) {
        if (_porPeriodo.TryGetValue(idPeriodo, out var info)) {
            return info.QuantidadeDias;
        }
        logger.LogWarning("Período {IdPeriodo} não encontrado no cache; usando {Default} dia(s) como padrão.", idPeriodo, defaultDias);
        return defaultDias;
    }

    public async Task RefreshAsync() {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoriaRepository>();
        var categorias = await repository.GetAllAsync(new FiltroCategoriaDTO { ApenasAtivos = false });
        var periodos = categorias
            .SelectMany(c => c.Periodos.Select(p =>
                new CategoriaPeriodoInfo(p.Id, p.QuantidadeDias, p.Valor, c.Id, c.Descricao)));
        AtualizarCache(periodos);
        logger.LogInformation("Cache de períodos atualizado: {Count} período(s).", _porPeriodo.Count);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ProximoTurnoApi.slnx --filter FullyQualifiedName~CategoriaPeriodoCacheTests`
Expected: PASS (3 testes).

- [ ] **Step 5: Commit**

```bash
git add Src/Infrastructure/Services/CategoriaPeriodoCache.cs Tests/Infrastructure/CategoriaPeriodoCacheTests.cs
git commit -m "feat: adiciona cache em memoria de categorias/periodos"
```

---

### Task 2: Warm-up + invalidação na infra + registro DI

**Files:**
- Create: `Src/Infrastructure/Services/CategoriaPeriodoCacheWarmup.cs`
- Modify: `Src/Infrastructure/Repositories/CategoriaRepository.cs`
- Modify: `Src/Program.cs:41` (região de registro de serviços)

**Interfaces:**
- Consumes: `ICategoriaPeriodoCache` (Task 1).
- Produces: `class CategoriaPeriodoCacheWarmup : IHostedService`.

- [ ] **Step 1: Create the warm-up hosted service**

```csharp
namespace ProximoTurnoApi.Infrastructure.Services;

public class CategoriaPeriodoCacheWarmup(
    ICategoriaPeriodoCache cache,
    ILogger<CategoriaPeriodoCacheWarmup> logger) : IHostedService {

    public async Task StartAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Aquecendo cache de períodos na inicialização.");
        await cache.RefreshAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 2: Inject the cache into `CategoriaRepository` and refresh after mutations**

Substituir o corpo de `CategoriaRepository` para receber o cache e invalidar após persistir:

```csharp
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ICategoriaRepository {
    Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro);
    Task<Categoria?> GetByIdAsync(int id);
    Task SaveAsync(Categoria categoria, bool commit = true);
    Task<bool> DeleteAsync(int id);
}

public class CategoriaRepository : BaseRepository, ICategoriaRepository {
    private readonly ICategoriaPeriodoCache _cache;

    public CategoriaRepository(DatabaseContext context, ICategoriaPeriodoCache cache) : base(context) {
        _cache = cache;
    }

    public async Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro) {
        var query = _dbContext.Categorias.AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Descricao)) {
            query = query.Where(c => c.Descricao.Contains(filtro.Descricao.ToLowerInvariant()));
        }

        if (filtro.ApenasAtivos) {
            query = query.Where(c => c.Ativo);
        }

        return await query
            .Include(c => c.Periodos)
            .ToListAsync();
    }

    public async Task<Categoria?> GetByIdAsync(int id) {
        return await _dbContext.Categorias
            .Include(c => c.Periodos)
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task SaveAsync(Categoria categoria, bool commit = true) {
        await SaveChangesAsync(_dbContext.Categorias, categoria, commit);
        if (commit) {
            await _cache.RefreshAsync();
        }
    }

    public async Task<bool> DeleteAsync(int id) {
        var afetadas = await _dbContext.Categorias
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ativo, false));
        if (afetadas > 0) {
            await _cache.RefreshAsync();
        }
        return afetadas > 0;
    }
}
```

- [ ] **Step 3: Register singleton + hosted service in `Program.cs`**

Após a linha `builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();` (linha ~41), adicionar:

```csharp
builder.Services.AddSingleton<ICategoriaPeriodoCache, CategoriaPeriodoCache>();
builder.Services.AddHostedService<CategoriaPeriodoCacheWarmup>();
```

Garantir o `using ProximoTurnoApi.Infrastructure.Services;` (já presente no topo do arquivo).

- [ ] **Step 4: Build**

Run: `dotnet build ProximoTurnoApi.slnx`
Expected: build succeeds (0 erros).

- [ ] **Step 5: Commit**

```bash
git add Src/Infrastructure/Services/CategoriaPeriodoCacheWarmup.cs Src/Infrastructure/Repositories/CategoriaRepository.cs Src/Program.cs
git commit -m "feat: warm-up e invalidacao automatica do cache de periodos"
```

---

### Task 3: Corrigir `Pedido.Entregar` (recálculo) + data opcional

**Files:**
- Modify: `Src/Domain/Pedido.cs:126-139` (`Entregar`), `:155-189` (`Renovar`)
- Test: `Tests/Domain/PedidoEntregaTests.cs` (novo)

**Interfaces:**
- Consumes: `ICategoriaPeriodoCache`, `CategoriaPeriodoInfo` (Task 1).
- Produces:
  - `bool Entregar(ICategoriaPeriodoCache cache, DateTime? dataDevolucao = null)`
  - `Pedido? Renovar(List<(int idItem, CategoriaPeriodoInfo periodo)?> itensRenovar, ICategoriaPeriodoCache cache)`

- [ ] **Step 1: Write the failing tests**

Criar `Tests/Domain/PedidoEntregaTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoEntregaTests {
    // Fake in-test do cache (sem lib de mock)
    private class FakeCache(int qtdeDias) : ICategoriaPeriodoCache {
        public bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info) {
            info = new CategoriaPeriodoInfo(idPeriodo, qtdeDias, 0m, 1, "cat");
            return true;
        }
        public int GetQuantidadeDias(int idPeriodo, int defaultDias = 1) => qtdeDias;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private static Pedido CriarPedidoComItem(int qtdeDias) {
        var cliente = new Cliente { Id = 1, Nome = "C", Email = "e", Telefone = "t", Endereco = "a" };
        var pedido = new Pedido(cliente, "dinheiro", "retirada");
        var item = new ItemPedido {
            Id = 1,
            IdPeriodo = 10,
            Valor = 50m,
            JogoCopia = new JogoCopia { Id = 1, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 5, Nome = "J", IdCategoria = 1 } },
            DataDevolucao = pedido.CalcularDataDevolucao(qtdeDias) // simula cálculo no cadastro (base = DataHora)
        };
        pedido.AdicionarItem(item);
        return pedido;
    }

    [Fact]
    public void Entregar_SemData_RecalculaDataDevolucaoComBaseNaEntrega() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);

        Assert.True(pedido.Entregar(cache));

        var esperado = DateTime.Now.Date.AddDays(5).AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public void Entregar_ComDataValida_AplicaMesmaDataATodosOsItens() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);
        var data = DateTime.Now.Date.AddDays(10);

        Assert.True(pedido.Entregar(cache, data));

        var esperado = data.AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public void Entregar_ComDataNoPassado_FalhaComNotificacao() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);

        var ok = pedido.Entregar(cache, DateTime.Now.Date); // hoje: inválido (não é > hoje)

        Assert.False(ok);
        Assert.False(pedido.IsValid);
        Assert.Equal(StatusPedido.Pendente, pedido.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ProximoTurnoApi.slnx --filter FullyQualifiedName~PedidoEntregaTests`
Expected: FAIL de compilação (`Entregar` não aceita esses parâmetros).

- [ ] **Step 3: Update `Pedido.Entregar`**

Substituir o método `Entregar` (linhas ~126-139) por:

```csharp
public bool Entregar(ICategoriaPeriodoCache cache, DateTime? dataDevolucao = null) {
    Clear();
    if (Status != StatusPedido.Pendente) {
        AddNotification("ERRO", $"Somente um pedido no status {StatusPedido.Pendente} pode ser entregue.");
        return false;
    }
    if (dataDevolucao.HasValue && dataDevolucao.Value.Date <= DateTime.Now.Date) {
        AddNotification("ERRO", "A data de devolução informada deve ser superior à data atual.");
        return false;
    }

    Status = StatusPedido.Entregue;
    DataHoraEntrega = DateTime.Now;

    foreach (var item in _items) {
        item.DataDevolucao = dataDevolucao.HasValue
            ? dataDevolucao.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
            : CalcularDataDevolucao(cache.GetQuantidadeDias(item.IdPeriodo));
        item.JogoCopia.Status = StatusJogo.Alugado;
    }
    RegistrarAlteracao();
    return true;
}
```

- [ ] **Step 4: Update `Pedido.Renovar` to thread the cache**

No método `Renovar` (linhas ~155-189):
1. Alterar a assinatura para `public Pedido? Renovar(List<(int idItem, CategoriaPeriodoInfo periodo)?> itensRenovar, ICategoriaPeriodoCache cache) {`.
2. Trocar `periodo.Id` por `periodo.IdPeriodo` e `periodo.Valor` / `periodo.QuantidadeDias` permanecem. Bloco do novo item:

```csharp
var novoItem = new ItemPedido() {
    IdJogoCopia = item.IdJogoCopia,
    JogoCopia = item.JogoCopia,
    IdPeriodo = itemRenovar.Value.periodo.IdPeriodo,
    Valor = itemRenovar.Value.periodo.Valor,
    DataDevolucao = novoPedido.CalcularDataDevolucao(itemRenovar.Value.periodo.QuantidadeDias),
    Renovado = true
};
```

3. Trocar `novoPedido.Entregar();` por `novoPedido.Entregar(cache);`.
4. Adicionar `using ProximoTurnoApi.Infrastructure.Services;` no topo de `Pedido.cs`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test ProximoTurnoApi.slnx --filter FullyQualifiedName~PedidoEntregaTests`
Expected: PASS (3 testes). Nota: `RenovarPedido` (Task 6) ainda pode não compilar até ser ajustado — se o build do projeto de testes falhar por causa disso, seguir para as Tasks 4-6 e rodar os testes ao final.

- [ ] **Step 6: Commit**

```bash
git add Src/Domain/Pedido.cs Tests/Domain/PedidoEntregaTests.cs
git commit -m "fix: recalcula data de devolucao na entrega e aceita data opcional"
```

---

### Task 4: `ValidarAdicionarItem` usa o cache

**Files:**
- Modify: `Src/Application/UseCases/Pedido/PedidoUseCaseBasico.cs`

**Interfaces:**
- Consumes: `ICategoriaPeriodoCache`, `CategoriaPeriodoInfo` (Task 1); `IJogoRepository`.
- Produces: `protected async Task<(JogoCopia copia, CategoriaPeriodoInfo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, ICategoriaPeriodoCache cache)`

- [ ] **Step 1: Rewrite `ValidarAdicionarItem`**

```csharp
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;

    protected async Task<(JogoCopia copia, CategoriaPeriodoInfo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, ICategoriaPeriodoCache cache) {
        var copias = await jogoRepository.GetAllCopiasByIdJogoAsync(item.IdJogo);
        var copia = copias?.FirstOrDefault(c => c.Status == StatusJogo.Disponivel);

        if (copia is null) {
            var jogoNaoDisp = await jogoRepository.GetByIdAsync(item.IdJogo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não há cópias disponíveis do jogo \"{jogoNaoDisp?.Nome ?? "desconhecido"}\""));
            return null;
        }

        // Descobre a categoria do jogo (carrega o jogo se necessário)
        var idCategoriaJogo = copia.Jogo?.IdCategoria;
        if (idCategoriaJogo is null) {
            var jogo = await jogoRepository.GetByIdAsync(item.IdJogo);
            idCategoriaJogo = jogo?.IdCategoria;
        }

        if (idCategoriaJogo is null || idCategoriaJogo == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Categoria do jogo não foi encontrada"));
            return null;
        }

        if (!cache.TryGetPeriodo(item.IdPeriodo, out var periodo) || periodo!.IdCategoria != idCategoriaJogo) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A categoria deste jogo não permite o período informado."));
            return null;
        }

        if (!IsValid)
            return null;

        return (copia, periodo);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build ProximoTurnoApi.slnx`
Expected: erros esperados em `CadastroPedido`/`AtualizarPedido`/`RenovarPedido` (ainda passando `ICategoriaRepository`). Serão corrigidos nas Tasks 5-6. Prosseguir.

- [ ] **Step 3: Commit**

```bash
git add Src/Application/UseCases/Pedido/PedidoUseCaseBasico.cs
git commit -m "refactor: ValidarAdicionarItem resolve periodo via cache"
```

---

### Task 5: Ajustar `CadastroPedido` e `AtualizarPedido`

**Files:**
- Modify: `Src/Application/UseCases/Pedido/CadastroPedido.cs:12-20,44-56`
- Modify: `Src/Application/UseCases/Pedido/AtualizarPedido.cs:7-12,36-58`

**Interfaces:**
- Consumes: `ValidarAdicionarItem(..., ICategoriaPeriodoCache)` (Task 4); `CategoriaPeriodoInfo` (`.IdPeriodo`, `.Valor`, `.QuantidadeDias`).

- [ ] **Step 1: `CadastroPedido` — trocar dependência e uso**

1. No construtor primário, remover `ICategoriaRepository _categoriaRepository,` e adicionar `ICategoriaPeriodoCache _categoriaPeriodoCache,`.
2. Adicionar `using ProximoTurnoApi.Infrastructure.Services;`.
3. Na chamada: `var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaPeriodoCache);`.
4. No `new ItemPedido`: trocar `IdPeriodo = resultValidacao.Value.periodo.Id,` por `IdPeriodo = resultValidacao.Value.periodo.IdPeriodo,` (mantendo `Valor` e `DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)`).

- [ ] **Step 2: `AtualizarPedido` — trocar dependência e uso**

1. No construtor, remover `ICategoriaRepository _categoriaRepository,` e adicionar `ICategoriaPeriodoCache _categoriaPeriodoCache,`.
2. Adicionar `using ProximoTurnoApi.Infrastructure.Services;`.
3. `var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaPeriodoCache);`.
4. No `new ItemPedido`: `IdPeriodo = resultValidacao.Value.periodo.IdPeriodo,` (linha ~55). `Valor`/`QuantidadeDias` permanecem.

- [ ] **Step 3: Build**

Run: `dotnet build ProximoTurnoApi.slnx`
Expected: resta apenas o erro em `RenovarPedido` (Task 6).

- [ ] **Step 4: Commit**

```bash
git add Src/Application/UseCases/Pedido/CadastroPedido.cs Src/Application/UseCases/Pedido/AtualizarPedido.cs
git commit -m "refactor: cadastro/atualizacao de pedido usam cache de periodos"
```

---

### Task 6: Ajustar `RenovarPedido`

**Files:**
- Modify: `Src/Application/UseCases/Pedido/RenovarPedido.cs`

**Interfaces:**
- Consumes: `ICategoriaPeriodoCache` (Task 1); `Pedido.Renovar(List<(int,CategoriaPeriodoInfo)?>, ICategoriaPeriodoCache)` (Task 3).

- [ ] **Step 1: Trocar repositório de categorias pelo cache**

1. No construtor, remover `ICategoriaRepository _categoriaRepository,` e adicionar `ICategoriaPeriodoCache _categoriaPeriodoCache,`.
2. Adicionar `using ProximoTurnoApi.Infrastructure.Services;`.
3. Remover a linha `var categorias = await _categoriaRepository.GetAllAsync(new FiltroCategoriaDTO());`.
4. Trocar o tipo da lista para `List<(int idItem, CategoriaPeriodoInfo periodo)?> itensNovoPedido = [];`.
5. Resolver o período via cache:

```csharp
if (!_categoriaPeriodoCache.TryGetPeriodo(novoItemDto.IdPeriodo, out var periodo) || periodo is null) {
    logger.LogWarning("Período de renovação ID {PeriodoId} inválido para o item {ItemId} do pedido {PedidoId}.", novoItemDto.IdPeriodo, itemRenovar.Id, idPedido);
    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não foi possível identificar qual o periodo para renovação."));
    continue;
}
itensNovoPedido.Add((itemRenovar.Id, periodo));
```

6. Trocar a chamada `pedidoExistente.Renovar(itensNovoPedido)` por `pedidoExistente.Renovar(itensNovoPedido, _categoriaPeriodoCache)`.

- [ ] **Step 2: Build**

Run: `dotnet build ProximoTurnoApi.slnx`
Expected: build succeeds (0 erros).

- [ ] **Step 3: Run full backend test suite**

Run: `dotnet test ProximoTurnoApi.slnx`
Expected: todos os testes passam (incluindo `PedidoEntregaTests` e `CategoriaPeriodoCacheTests`).

- [ ] **Step 4: Commit**

```bash
git add Src/Application/UseCases/Pedido/RenovarPedido.cs
git commit -m "refactor: renovacao de pedido usa cache de periodos"
```

---

### Task 7: DTO + Controller + Use case de status

**Files:**
- Modify: `Src/Application/DTOs/StatusPedidoDTO.cs`
- Modify: `Src/Application/UseCases/Pedido/AtualizarStatusPedido.cs`
- Modify: `Src/Application/Controllers/PedidosController.cs:81-90`

**Interfaces:**
- Consumes: `ICategoriaPeriodoCache` (Task 1); `Pedido.Entregar(cache, dataDevolucao?)` (Task 3).
- Produces: `StatusPedidoDTO { StatusPedido Status; DateTime? DataDevolucao }`; `AtualizarStatusPedido.ExecuteAsync(int idPedido, StatusPedido novoStatus, DateTime? dataDevolucao = null)`.

- [ ] **Step 1: Adicionar `DataDevolucao` ao DTO**

```csharp
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public record StatusPedidoDTO {
    public StatusPedido Status { get; set; }
    public DateTime? DataDevolucao { get; set; }
}
```

- [ ] **Step 2: Injetar o cache e propagar a data no `AtualizarStatusPedido`**

1. Construtor: `AtualizarStatusPedido(IPedidoRepository pedidoRepository, ICategoriaPeriodoCache categoriaPeriodoCache, ILogger<AtualizarStatusPedido> logger)`.
2. Adicionar `using ProximoTurnoApi.Infrastructure.Services;`.
3. Assinatura: `public async Task ExecuteAsync(int idPedido, StatusPedido novoStatus, DateTime? dataDevolucao = null)`.
4. No ramo de entrega, trocar `pedidoExistente.Entregar()` por `pedidoExistente.Entregar(categoriaPeriodoCache, dataDevolucao)`.

- [ ] **Step 3: Propagar a data no controller**

Em `PedidosController.AtualizarStatusPedido` (linha ~84):
```csharp
await _atualizarStatusPedidoUseCase.ExecuteAsync(id, novoStatus.Status, novoStatus.DataDevolucao);
```
O `DELETE` (cancelamento, linha ~119) permanece `ExecuteAsync(id, StatusPedido.Cancelado)`.

- [ ] **Step 4: Build + testes**

Run: `dotnet build ProximoTurnoApi.slnx` e `dotnet test ProximoTurnoApi.slnx`
Expected: build ok; testes passam.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/DTOs/StatusPedidoDTO.cs Src/Application/UseCases/Pedido/AtualizarStatusPedido.cs Src/Application/Controllers/PedidosController.cs
git commit -m "feat: endpoint de status aceita data de devolucao opcional na entrega"
```

---

### Task 8: Frontend — data opcional no diálogo de entrega

**Files:**
- Modify: `ProximoTurno/lib/api-service.ts:163-165` (`interface StatusPedido`)
- Modify: `ProximoTurno/app/pedidos/page.tsx` (estado + `handleConfirmarEntrega` + diálogo "Confirmar Entrega")

**Interfaces:**
- Consumes: `apiService.atualizarStatusPedido(idPedido, { status, dataDevolucao? })`.

- [ ] **Step 1: Estender a interface `StatusPedido`**

Em `lib/api-service.ts`:
```typescript
export interface StatusPedido {
    status: number
    dataDevolucao?: string
}
```

- [ ] **Step 2: Estado do input no `pedidos/page.tsx`**

Junto aos estados de entrega (linha ~72), adicionar:
```typescript
const [dataDevolucaoEntrega, setDataDevolucaoEntrega] = useState<string>("")
```
Em `handleEntregar` (linha ~165), resetar ao abrir:
```typescript
const handleEntregar = (pedido: Pedido) => {
    setPedidoParaEntregar(pedido)
    setDataDevolucaoEntrega("")
    setIsConfirmDeliverOpen(true)
}
```

- [ ] **Step 3: Validar e enviar em `handleConfirmarEntrega`**

Substituir o corpo (linhas ~170-187):
```typescript
const handleConfirmarEntrega = async () => {
    if (!pedidoParaEntregar) return

    if (dataDevolucaoEntrega) {
        const hoje = new Date()
        hoje.setHours(0, 0, 0, 0)
        const escolhida = new Date(`${dataDevolucaoEntrega}T00:00:00`)
        if (escolhida <= hoje) {
            toast.error("A data de devolução deve ser posterior a hoje.")
            return
        }
    }

    setProcessingDeliver(true)
    try {
        await apiService.atualizarStatusPedido(pedidoParaEntregar.id, {
            status: 1,
            ...(dataDevolucaoEntrega ? { dataDevolucao: dataDevolucaoEntrega } : {}),
        })
        toast.success("Pedido entregue com sucesso!")
        setIsConfirmDeliverOpen(false)
        fetchPedidos()
    } catch (error) {
        console.error("Erro ao entregar pedido:", error)
        toast.error("Erro ao entregar pedido. Tente novamente.")
    } finally {
        setProcessingDeliver(false)
    }
}
```

- [ ] **Step 4: Campo de data no diálogo "Confirmar Entrega"**

No `<div className="py-2">` do diálogo (linha ~905), após o parágrafo existente, adicionar (reusando `Input type="date"`, `Label`, `Calendar`, já importados):
```tsx
<div className="mt-4 space-y-2">
    <Label htmlFor="data-devolucao-entrega">Data de devolução (opcional)</Label>
    <div className="relative">
        <Calendar className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
        <Input
            id="data-devolucao-entrega"
            type="date"
            className="pl-10"
            value={dataDevolucaoEntrega}
            min={new Date(Date.now() + 86400000).toISOString().split("T")[0]}
            onChange={(e) => setDataDevolucaoEntrega(e.target.value)}
        />
    </div>
    <p className="text-xs text-muted-foreground">
        Se não informado, será calculado automaticamente a partir da data de entrega.
    </p>
</div>
```

- [ ] **Step 5: Verificar build do frontend**

Run (em `ProximoTurno/`): `npm run build` (ou `pnpm build`)
Expected: build sem erros de tipo.

- [ ] **Step 6: Commit**

```bash
git add lib/api-service.ts app/pedidos/page.tsx
git commit -m "feat: campo opcional de data de devolucao no dialogo de entrega"
```

---

## Self-Review

- **Cobertura do spec:** cache (T1), warm-up/invalidação/DI (T2), correção `Entregar` + data opcional (T3), `ValidarAdicionarItem` via cache (T4), cadastro/atualização (T5), renovação (T6), DTO/controller/status (T7), frontend (T8). ✅
- **Fallback default=1 + warning:** implementado em `GetQuantidadeDias` (T1) e exercitado indiretamente por `Entregar` sem período. ✅
- **Regra de validação `> hoje`:** backend (T3) e frontend (T8). ✅
- **Sem migração:** confirmado. ✅
- **Consistência de tipos:** `CategoriaPeriodoInfo.IdPeriodo/QuantidadeDias/Valor/IdCategoria/DescricaoCategoria` usados de forma consistente em T3-T7; retorno de `ValidarAdicionarItem` alinhado com os chamadores. ✅
