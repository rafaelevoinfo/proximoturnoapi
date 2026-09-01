# Exclusão de conta do cliente (LGPD) — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que o titular exclua a própria conta — anonimizando o `CLIENTE`, apagando comentários, lista de desejos, contratos locais e o login do Identity — preservando o histórico de pedidos.

**Architecture:** Um use case `ExcluirContaCliente` orquestra guardas + anonimização dentro de uma única transação do `DatabaseContext` (que é também o store do Identity, então o delete do `IdentityUser` entra na mesma transação). Exposto por `DELETE /api/clientes/{id}/conta`. O frontend consome via proxy Next e um dialog no perfil.

**Tech Stack:** .NET 10, EF Core (MySQL), ASP.NET Core Identity, Flunt (notification pattern), xUnit com fakes escritos à mão · Next.js 16, React 19, Radix UI, sonner.

**Spec:** `ProximoTurnoApi/docs/superpowers/specs/2026-08-25-exclusao-conta-lgpd-design.md`

## Global Constraints

- **Dois repositórios git separados.** Tasks 1–6 são em `D:\Repositorios\ProximoTurno\ProximoTurnoApi` (repo `ProximoTurnoApi`). Tasks 7–9 são em `D:\Repositorios\ProximoTurno\ProximoTurno` (repo `ProximoTurno`). A pasta pai **não** é um repo. Confira `git rev-parse --show-toplevel` antes de cada `git add`.
- **Nunca use PowerShell `Get-Content`/`Set-Content` para editar fontes deste projeto** — corrompe os acentos UTF-8. Use a ferramenta Edit/Write ou Node.
- **Invariante do domínio:** `DataAnonimizacao != null ⇒ Ativo == false`. A recíproca não vale.
- **Tokens de anonimização precisam ser únicos por linha.** `CLIENTE.EMAIL`, `CLIENTE.TELEFONE` e `CLIENTE.CPF` são índices UNIQUE (`DatabaseContext.cs:24,25,28`). Valores exatos, em minúsculo (os setters de `Nome` e `Email` aplicam `ToLowerInvariant()`):
  - `Nome` → `"cliente removido"`
  - `Email` → `$"anon-{id}@removido.local"`
  - `Telefone` → `$"anon{id}"`
  - `Endereco` → `"removido"`
  - `Cpf`, `DataNascimento`, `ComoNosConheceu` → `null`
- **O documento no Autentique NÃO é excluído.** Apenas a linha local de `CONTRATO_AUTENTIQUE`. Decisão consciente registrada na spec (decisão 5) — não "corrija" isso.
- **Mensagem de recusa para conta Admin, literal:** `"Contas com perfil de administrador não podem ser excluídas por aqui. Peça a outro administrador para remover seu perfil de administrador e repita a exclusão."`
- **Build/test backend:** `dotnet build` e `dotnet test Tests/Tests.csproj` a partir de `ProximoTurnoApi/`.
- **Commits:** o repo `ProximoTurnoApi` está em `main`. Crie a branch `feat/exclusao-conta-lgpd` antes do primeiro commit; idem no repo do frontend antes da Task 8.

---

## Estrutura de arquivos

**Backend (`ProximoTurnoApi`):**

| Arquivo | Responsabilidade |
|---|---|
| `Src/Infrastructure/Models/Cliente.cs` | + propriedade `DataAnonimizacao` |
| `Src/Migrations/<ts>_AddDataAnonimizacaoToCliente.cs` | coluna nova |
| `Src/Application/DTOs/ClienteDTO.cs` | + `DataAnonimizacao` para o grid admin |
| `Src/Application/DTOs/ExcluirContaDTOs.cs` | **novo** — request (senha) e payload do 409 |
| `Src/Infrastructure/Repositories/PedidoRepository.cs` | + `ObterPedidosEmAbertoAsync` |
| `Src/Infrastructure/Repositories/ClienteRepository.cs` | + `ExcluirDadosVinculadosAsync` |
| `Src/Infrastructure/Repositories/ContratoRepository.cs` | + `ExcluirPorClienteAsync` |
| `Src/Application/UseCases/Cliente/ExcluirContaCliente.cs` | **novo** — guardas + anonimização |
| `Src/Application/Controllers/ClientesController.cs` | + endpoint `DELETE {id}/conta` |
| `Src/Program.cs` | + registro `Scoped` |
| `Src/Application/UseCases/Cliente/EnviarEmailsClientes.cs` | pular anonimizados |
| `Src/Application/UseCases/Comentario/ObterComentariosJogo.cs` | filtrar anonimizados |
| `Tests/Fakes/PedidoUseCaseFakes.cs` | métodos novos nos fakes |
| `Tests/Domain/ExcluirContaClienteTests.cs` | **novo** |

**Frontend (`ProximoTurno`):**

| Arquivo | Responsabilidade |
|---|---|
| `lib/api-service.ts` | `ApiRequestError` com status, `excluirConta`, campo `dataAnonimizacao` |
| `app/api/clientes/[id]/conta/route.ts` | **novo** — proxy DELETE |
| `components/excluir-conta-dialog.tsx` | **novo** — confirmação + senha + estado 409 |
| `app/perfil/page.tsx` | seção "Zona de risco" |
| `app/admin/clientes/page.tsx` | ação "Excluir conta (LGPD)" + badge de conta excluída |

---

## Task 1: Coluna `DATA_ANONIMIZACAO`

**Repo:** `ProximoTurnoApi`

**Files:**
- Modify: `Src/Infrastructure/Models/Cliente.cs`
- Modify: `Src/Application/DTOs/ClienteDTO.cs`
- Create: `Src/Migrations/<timestamp>_AddDataAnonimizacaoToCliente.cs` (gerado)

**Interfaces:**
- Produces: `Cliente.DataAnonimizacao` (`DateTime?`), `ClienteDTO.DataAnonimizacao` (`DateTime?`)

- [ ] **Step 1: Criar a branch**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurnoApi
git checkout -b feat/exclusao-conta-lgpd
```

- [ ] **Step 2: Adicionar a propriedade ao modelo**

Em `Src/Infrastructure/Models/Cliente.cs`, logo após a propriedade `Ativo`:

```csharp
    /// <summary>
    /// Preenchido quando o titular exerce o direito de eliminação (LGPD Art. 18, VI).
    /// Null = conta nunca excluída. Invariante: preenchido implica Ativo == false.
    /// </summary>
    [Column("DATA_ANONIMIZACAO")]
    public DateTime? DataAnonimizacao { get; set; }
```

- [ ] **Step 3: Expor no DTO**

Em `Src/Application/DTOs/ClienteDTO.cs`, logo após `LoginAtivo`:

```csharp
    /// <summary>Somente leitura: quando a conta foi excluída pelo titular. Null = conta ativa.</summary>
    public DateTime? DataAnonimizacao { get; set; }
```

E no corpo de `ClienteDTO.FromModel`, junto às demais atribuições:

```csharp
            DataAnonimizacao = model.DataAnonimizacao,
```

- [ ] **Step 4: Gerar a migration**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurnoApi
dotnet ef migrations add AddDataAnonimizacaoToCliente --project Src/ProximoTurnoApi.csproj
```

Esperado: dois arquivos novos em `Src/Migrations/` e o snapshot atualizado.

- [ ] **Step 5: Conferir o conteúdo gerado**

Abra o `.cs` da migration. O `Up` deve conter exatamente uma `AddColumn<DateTime>` para `DATA_ANONIMIZACAO` na tabela `CLIENTE`, com `nullable: true`. Se trouxer qualquer outra alteração, o snapshot estava desatualizado — pare e investigue antes de seguir.

- [ ] **Step 6: Compilar**

```bash
dotnet build
```

Esperado: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add Src/Infrastructure/Models/Cliente.cs Src/Application/DTOs/ClienteDTO.cs Src/Migrations/
git commit -m "feat(lgpd): adiciona DATA_ANONIMIZACAO ao cliente"
```

---

## Task 2: Métodos de repositório

**Repo:** `ProximoTurnoApi`

**Files:**
- Modify: `Src/Infrastructure/Repositories/PedidoRepository.cs`
- Modify: `Src/Infrastructure/Repositories/ClienteRepository.cs`
- Modify: `Src/Infrastructure/Repositories/ContratoRepository.cs`
- Modify: `Tests/Fakes/PedidoUseCaseFakes.cs`

**Interfaces:**
- Consumes: `Cliente.DataAnonimizacao` (Task 1)
- Produces:
  - `IPedidoRepository.ObterPedidosEmAbertoAsync(int idCliente) → Task<List<Pedido>>`
  - `IClienteRepository.ExcluirDadosVinculadosAsync(int idCliente) → Task`
  - `IContratoRepository.ExcluirPorClienteAsync(int idCliente) → Task`
  - Fakes com listas públicas: `FakePedidoRepository.Pedidos`, `FakeClienteRepository.DadosVinculadosExcluidos` (`List<int>`), `FakeContratoRepository.ContratosExcluidosPorCliente` (`List<int>`)

- [ ] **Step 1: Adicionar `ObterPedidosEmAbertoAsync`**

Na interface `IPedidoRepository` em `Src/Infrastructure/Repositories/PedidoRepository.cs`:

```csharp
    /// <summary>Pedidos que impedem a exclusão da conta: ainda não devolvidos nem cancelados.</summary>
    Task<List<Pedido>> ObterPedidosEmAbertoAsync(int idCliente);
```

E na classe `PedidoRepository`:

```csharp
    public async Task<List<Pedido>> ObterPedidosEmAbertoAsync(int idCliente) {
        return await _dbContext.Pedidos
            .AsNoTracking()
            .Include(p => p.Items)!
                .ThenInclude(i => i.JogoCopia)
                    .ThenInclude(jc => jc.Jogo)
            .Where(p => p.Cliente.Id == idCliente
                     && (p.Status == StatusPedido.Pendente || p.Status == StatusPedido.Entregue))
            .ToListAsync();
    }
```

- [ ] **Step 2: Adicionar `ExcluirDadosVinculadosAsync`**

Na interface `IClienteRepository` em `Src/Infrastructure/Repositories/ClienteRepository.cs`:

```csharp
    /// <summary>Apaga comentários e itens de lista de desejos do cliente. Usado na exclusão de conta (LGPD).</summary>
    Task ExcluirDadosVinculadosAsync(int idCliente);
```

E na classe `ClienteRepository`:

```csharp
    public async Task ExcluirDadosVinculadosAsync(int idCliente) {
        // Hard delete: o texto livre do comentário pode conter dado pessoal que anonimizar o
        // autor não removeria. Ver decisão 3 da spec.
        await _dbContext.Comentarios
            .Where(c => c.IdCliente == idCliente)
            .ExecuteDeleteAsync();

        await _dbContext.ItensListaDesejos
            .Where(i => i.IdCliente == idCliente)
            .ExecuteDeleteAsync();
    }
```

- [ ] **Step 3: Adicionar `ExcluirPorClienteAsync`**

Na interface `IContratoRepository` em `Src/Infrastructure/Repositories/ContratoRepository.cs`:

```csharp
    /// <summary>
    /// Apaga as linhas locais de contrato dos pedidos do cliente. O documento no Autentique
    /// permanece — risco residual assumido, ver decisão 5 da spec.
    /// </summary>
    Task ExcluirPorClienteAsync(int idCliente);
```

E na classe `ContratoRepository`:

```csharp
    public async Task ExcluirPorClienteAsync(int idCliente) {
        var idsPedidos = _dbContext.Pedidos
            .Where(p => p.Cliente.Id == idCliente)
            .Select(p => p.Id);

        await _dbContext.ContratosAutentique
            .Where(c => idsPedidos.Contains(c.IdPedido))
            .ExecuteDeleteAsync();
    }
```

- [ ] **Step 4: Atualizar TODAS as implementações dos fakes**

⚠️ **Atenção — isto é maior do que parece.** Além dos fakes compartilhados em `Tests/Fakes/PedidoUseCaseFakes.cs`, vários arquivos de teste declaram **suas próprias cópias privadas aninhadas** dessas interfaces. Adicionar um método à interface quebra todas. São **12 implementações em 6 arquivos**:

| Interface | Método novo | Implementações |
|---|---|---|
| `IClienteRepository` | `ExcluirDadosVinculadosAsync` | `Tests/Fakes/PedidoUseCaseFakes.cs:134` · `Tests/Domain/ConsultarContratoPedidoTests.cs:51` · `Tests/Domain/EnviarEmailsClientesTests.cs:114` · `Tests/Domain/GerarContratoPedidoTests.cs:52` |
| `IPedidoRepository` | `ObterPedidosEmAbertoAsync` | `Tests/Fakes/PedidoUseCaseFakes.cs:30` · `Tests/Domain/ConsultarContratoPedidoTests.cs:77` · `Tests/Domain/GerarContratoPedidoTests.cs:78` |
| `IContratoRepository` | `ExcluirPorClienteAsync` | `Tests/Fakes/PedidoUseCaseFakes.cs:157` · `Tests/Domain/ConsultarContratoPedidoTests.cs:20` · `Tests/Domain/ContratoQueueBackgroundServiceTests.cs:161` · `Tests/Domain/GerarContratoPedidoTests.cs:21` · `Tests/Domain/ProcessarWebhookAutentiqueTests.cs:14` |

**Nas cópias privadas aninhadas** (todas fora de `Tests/Fakes/`), o método novo é um stub — elas nunca exercitam exclusão de conta, e esse é o padrão que já usam para o que não exercitam:

```csharp
    public Task ExcluirDadosVinculadosAsync(int idCliente) => throw new NotImplementedException();
```

```csharp
    public Task<List<Pedido>> ObterPedidosEmAbertoAsync(int idCliente) => throw new NotImplementedException();
```

```csharp
    public Task ExcluirPorClienteAsync(int idCliente) => throw new NotImplementedException();
```

Confira o estilo de cada arquivo antes de colar — alguns usam corpo em bloco com `{ }` em vez de corpo de expressão.

**Nos fakes compartilhados** de `Tests/Fakes/PedidoUseCaseFakes.cs`, implementação real.

Em `FakePedidoRepository` (que já expõe `public List<Pedido> Pedidos { get; set; }` na linha 31):

```csharp
    public Task<List<Pedido>> ObterPedidosEmAbertoAsync(int idCliente) =>
        Task.FromResult(Pedidos
            .Where(p => p.Cliente.Id == idCliente
                     && (p.Status == StatusPedido.Pendente || p.Status == StatusPedido.Entregue))
            .ToList());
```

Em `FakeClienteRepository`:

```csharp
    public List<int> DadosVinculadosExcluidos { get; } = [];

    public Task ExcluirDadosVinculadosAsync(int idCliente) {
        DadosVinculadosExcluidos.Add(idCliente);
        return Task.CompletedTask;
    }
```

Troque também o `UpdateAsync` de `FakeClienteRepository`, que hoje é `throw new NotImplementedException()`, porque o use case vai chamá-lo:

```csharp
    public Task UpdateAsync(Cliente cliente) => Task.CompletedTask;
```

Em `FakeContratoRepository`:

```csharp
    public List<int> ContratosExcluidosPorCliente { get; } = [];

    public Task ExcluirPorClienteAsync(int idCliente) {
        ContratosExcluidosPorCliente.Add(idCliente);
        return Task.CompletedTask;
    }
```

- [ ] **Step 5: Compilar e rodar a suíte existente**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurnoApi
dotnet build && dotnet test Tests/Tests.csproj
```

Esperado: build OK e todos os testes que já passavam continuam passando. Se `FakePedidoRepository` não expunha `Pedidos` publicamente, ajuste até compilar.

- [ ] **Step 6: Commit**

```bash
git add Src/Infrastructure/Repositories/ Tests/Fakes/PedidoUseCaseFakes.cs
git commit -m "feat(lgpd): metodos de repositorio para exclusao de conta"
```

---

## Task 3: `ExcluirContaCliente` — guardas

Entrega: o use case recusa corretamente todos os casos proibidos. O caminho feliz ainda não anonimiza nada (vem na Task 4).

**Repo:** `ProximoTurnoApi`

**Files:**
- Create: `Src/Application/DTOs/ExcluirContaDTOs.cs`
- Create: `Src/Application/UseCases/Cliente/ExcluirContaCliente.cs`
- Create: `Tests/Domain/ExcluirContaClienteTests.cs`
- Modify: `Tests/Fakes/PedidoUseCaseFakes.cs`

**Interfaces:**
- Consumes: os três métodos da Task 2; `Cliente.DataAnonimizacao` (Task 1)
- Produces:
  - `ExcluirContaCliente.ExecuteAsync(int idCliente, string? senha, bool solicitadoPorAdmin) → Task<bool>`
  - `ExcluirContaCliente.PedidosEmAberto` → `IReadOnlyList<PedidoEmAbertoDTO>` (vazia quando o motivo da recusa não é pedido em aberto)
  - `record PedidoEmAbertoDTO(int Id, DateTime DataHora, List<string> Jogos)`
  - `class ExcluirContaRequestDTO { string? Senha { get; set; } }`

- [ ] **Step 1: Criar os DTOs**

`Src/Application/DTOs/ExcluirContaDTOs.cs`:

```csharp
namespace ProximoTurnoApi.Application.DTOs;

/// <summary>Corpo do DELETE /api/clientes/{id}/conta. Senha é obrigatória quando o próprio cliente solicita.</summary>
public class ExcluirContaRequestDTO {
    public string? Senha { get; set; }
}

/// <summary>Pedido que impede a exclusão, devolvido no 409 para o frontend montar a tela.</summary>
public record PedidoEmAbertoDTO(int Id, DateTime DataHora, List<string> Jogos);
```

- [ ] **Step 2: Estender `FakeUserManager`**

Em `Tests/Fakes/PedidoUseCaseFakes.cs`, substitua `FakeUserManager` por:

```csharp
public class FakeUserManager : UserManager<Usuario> {
    private readonly Usuario? _user;
    private readonly bool _isAdmin;

    /// <summary>Usuário devolvido por FindByEmailAsync. Null simula cliente importado sem login.</summary>
    public Usuario? UsuarioPorEmail { get; set; }
    /// <summary>Resultado de CheckPasswordAsync.</summary>
    public bool SenhaCorreta { get; set; } = true;
    /// <summary>Ids dos usuários passados para DeleteAsync.</summary>
    public List<string> Deletados { get; } = [];

    public FakeUserManager(Usuario? user, bool isAdmin = false)
        : base(new FakeUserStore(), null!, null!, null!, null!, null!, null!, null!, null!) {
        _user = user;
        _isAdmin = isAdmin;
        UsuarioPorEmail = user;
    }

    public override Task<Usuario?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(_user);
    public override Task<bool> IsInRoleAsync(Usuario user, string role) => Task.FromResult(_isAdmin);
    public override Task<Usuario?> FindByEmailAsync(string email) => Task.FromResult(UsuarioPorEmail);
    public override Task<bool> CheckPasswordAsync(Usuario user, string password) => Task.FromResult(SenhaCorreta);

    public override Task<IdentityResult> DeleteAsync(Usuario user) {
        Deletados.Add(user.Id);
        return Task.FromResult(IdentityResult.Success);
    }
}
```

- [ ] **Step 3: Escrever os testes de guarda (vão falhar)**

`Tests/Domain/ExcluirContaClienteTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Tests.Fakes;

namespace ProximoTurnoApi.Tests.Domain;

public class ExcluirContaClienteTests {

    private static Cliente NovoCliente(int id = 1) => new() {
        Id = id,
        Nome = "ana silva",
        Email = "ana@x.com",
        Telefone = "11999998888",
        Endereco = "rua das flores, 10",
        Cpf = "12345678901",
        DataNascimento = new DateOnly(1990, 5, 20),
        ComoNosConheceu = "instagram",
        AceitaReceberOfertas = true,
        Ativo = true
    };

    private static (ExcluirContaCliente useCase,
                    FakeClienteRepository clientes,
                    FakePedidoRepository pedidos,
                    FakeContratoRepository contratos,
                    FakeUserManager users,
                    FakeEmailService email) Montar(
        Cliente? cliente = null,
        bool isAdmin = false) {

        cliente ??= NovoCliente();
        var clientes = new FakeClienteRepository { Clientes = { cliente } };
        var pedidos = new FakePedidoRepository();
        var contratos = new FakeContratoRepository();
        var usuario = new Usuario { Id = "u1", Email = cliente.Email, Nome = cliente.Nome };
        var users = new FakeUserManager(usuario, isAdmin);
        var email = new FakeEmailService();

        var useCase = new ExcluirContaCliente(
            clientes, pedidos, contratos, users, email,
            NullLogger<ExcluirContaCliente>.Instance);

        return (useCase, clientes, pedidos, contratos, users, email);
    }

    // Pedido.Status tem setter privado. O caminho é o mesmo de PedidoTests.cs: montar o pedido
    // pelo domínio. Nunca abra o setter só para o teste.
    private static Pedido PedidoPendente(Cliente cliente, int idItem = 1) {
        var pedido = new Pedido(cliente);
        pedido.AdicionarItem(new ItemPedido {
            Id = idItem,
            JogoCopia = new JogoCopia {
                Id = idItem,
                Status = StatusJogo.Disponivel,
                Jogo = new Jogo { Id = 10, Nome = "Catan", IdCategoria = 1 }
            },
            IdPeriodo = 1,
            Valor = 50m
        });
        return pedido;
    }

    private static Pedido PedidoEntregue(Cliente cliente, int idItem = 1) {
        var pedido = PedidoPendente(cliente, idItem);
        pedido.Entregar(new FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));
        return pedido;
    }

    private static Pedido PedidoDevolvido(Cliente cliente, int idItem = 1) {
        var pedido = PedidoEntregue(cliente, idItem);
        pedido.Devolver(null);
        return pedido;
    }

    // FakeEmailService de EnviarEmailsClientesTests é uma classe privada aninhada e não pode ser
    // reusada aqui. Esta é a cópia local, com a flag de falha que o teste de SMTP precisa.
    private class FakeEmailService : IEmailService {
        public List<(string to, string subject, string body)> Enviados { get; } = [];
        public bool LancarErro { get; set; }

        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true) {
            if (LancarErro) {
                throw new InvalidOperationException("smtp fora do ar");
            }
            Enviados.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Recusa_QuandoClienteNaoExiste() {
        var (useCase, _, _, _, _, _) = Montar();

        var ok = await useCase.ExecuteAsync(999, "senha", solicitadoPorAdmin: false);

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.NotFound);
    }

    [Fact]
    public async Task Idempotente_QuandoJaAnonimizado() {
        var cliente = NovoCliente();
        cliente.DataAnonimizacao = new DateTime(2026, 1, 1);
        cliente.Ativo = false;
        var (useCase, _, _, _, _, _) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.True(ok);
        Assert.Empty(useCase.Notifications);
    }

    [Fact]
    public async Task Recusa_QuandoSenhaIncorreta() {
        var (useCase, _, _, _, users, _) = Montar();
        users.SenhaCorreta = false;

        var ok = await useCase.ExecuteAsync(1, "errada", solicitadoPorAdmin: false);

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.BadRequest);
    }

    [Fact]
    public async Task NaoPedeSenha_QuandoSolicitadoPorAdmin() {
        var (useCase, _, _, _, users, _) = Montar();
        users.SenhaCorreta = false;

        var ok = await useCase.ExecuteAsync(1, senha: null, solicitadoPorAdmin: true);

        Assert.True(ok);
    }

    [Fact]
    public async Task Recusa_QuandoAlvoEhAdmin() {
        var (useCase, _, _, _, _, _) = Montar(isAdmin: true);

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n =>
            n.Message.Contains("perfil de administrador"));
    }

    [Fact]
    public async Task Recusa_ComPedidoPendente() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoPendente(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.False(ok);
        Assert.Single(useCase.PedidosEmAberto);
        Assert.Contains("Catan", useCase.PedidosEmAberto[0].Jogos);
    }

    [Fact]
    public async Task Recusa_ComPedidoEntregue() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoEntregue(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.False(ok);
        Assert.Single(useCase.PedidosEmAberto);
    }

    [Fact]
    public async Task Permite_QuandoTodosOsPedidosForamDevolvidos() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoDevolvido(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.True(ok);
        Assert.Empty(useCase.PedidosEmAberto);
    }

    [Fact]
    public async Task Permite_QuandoSemPedidoNenhum() {
        var (useCase, _, _, _, _, _) = Montar();

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false);

        Assert.True(ok);
        Assert.Empty(useCase.PedidosEmAberto);
    }
}
```

Nota para quem implementar: os helpers `PedidoPendente`/`PedidoEntregue`/`PedidoDevolvido` acima montam o pedido pelo domínio (`AdicionarItem` → `Entregar` → `Devolver`), que é o mesmo caminho de `Tests/Domain/PedidoTests.cs:139-149`. `Pedido.Status` tem setter privado — **não** abra o setter para o teste.

- [ ] **Step 4: Rodar os testes para confirmar que falham**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurnoApi
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~ExcluirContaCliente"
```

Esperado: falha de compilação — `ExcluirContaCliente` não existe.

- [ ] **Step 5: Implementar o use case com as guardas**

`Src/Application/UseCases/Cliente/ExcluirContaCliente.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Atende o direito de eliminação do titular (LGPD Art. 18, VI): anonimiza o cliente,
/// apaga comentários, lista de desejos, contratos locais e o login do Identity.
/// O histórico de pedidos é preservado por obrigação fiscal (Art. 16, I).
/// </summary>
public class ExcluirContaCliente(
    IClienteRepository clienteRepository,
    IPedidoRepository pedidoRepository,
    IContratoRepository contratoRepository,
    UserManager<Usuario> userManager,
    IEmailService emailService,
    ILogger<ExcluirContaCliente> logger) : UseCaseBasico {

    private const string MensagemAdmin =
        "Contas com perfil de administrador não podem ser excluídas por aqui. " +
        "Peça a outro administrador para remover seu perfil de administrador e repita a exclusão.";

    /// <summary>Preenchido apenas quando a recusa foi por pedidos em aberto.</summary>
    public IReadOnlyList<PedidoEmAbertoDTO> PedidosEmAberto { get; private set; } = [];

    public async Task<bool> ExecuteAsync(int idCliente, string? senha, bool solicitadoPorAdmin) {
        logger.LogInformation("Iniciando exclusão de conta do cliente {ClienteId}.", idCliente);
        PedidosEmAberto = [];

        var cliente = await clienteRepository.GetByIdAsync(idCliente);
        if (cliente is null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound, $"Cliente de id {idCliente} não encontrado."));
            return false;
        }

        // Idempotente: repetir a exclusão de uma conta já excluída é sucesso, não erro.
        if (cliente.DataAnonimizacao is not null) {
            logger.LogInformation("Cliente {ClienteId} já estava anonimizado.", idCliente);
            return true;
        }

        var usuarioCliente = await userManager.FindByEmailAsync(cliente.Email);

        // Senha só é exigida do próprio titular. A autenticação de admin já é o controle no
        // caminho administrativo. Sem isso, uma sessão sequestrada apagaria a conta.
        if (!solicitadoPorAdmin) {
            if (usuarioCliente is null || string.IsNullOrEmpty(senha)
                || !await userManager.CheckPasswordAsync(usuarioCliente, senha)) {
                AddNotification(UseCaseNotification.Create(
                    UseCaseNotificationType.BadRequest, "Senha incorreta."));
                return false;
            }
        }

        var pedidosAbertos = await pedidoRepository.ObterPedidosEmAbertoAsync(idCliente);
        if (pedidosAbertos.Count > 0) {
            PedidosEmAberto = [.. pedidosAbertos.Select(p => new PedidoEmAbertoDTO(
                p.Id,
                p.DataHora,
                [.. p.Items.Select(i => i.JogoCopia?.Jogo?.Nome ?? "jogo")]))];

            logger.LogInformation(
                "Exclusão recusada: cliente {ClienteId} tem {Qtde} pedido(s) em aberto.",
                idCliente, pedidosAbertos.Count);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Existem pedidos em aberto. Devolva os jogos antes de excluir a conta."));
            return false;
        }

        // Guarda técnica, não isenção de LGPD: deletar o IdentityUser leva junto o vínculo de
        // role, e perder a última conta Admin exige recuperação manual no banco. Ver decisão 6.
        if (usuarioCliente is not null && await userManager.IsInRoleAsync(usuarioCliente, Roles.Admin)) {
            logger.LogWarning("Exclusão recusada: cliente {ClienteId} tem perfil de administrador.", idCliente);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, MensagemAdmin));
            return false;
        }

        return true;
    }
}
```

- [ ] **Step 6: Rodar os testes de guarda**

```bash
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~ExcluirContaCliente"
```

Esperado: todos PASS. `Permite_QuandoSemPedidoNenhum` e `NaoPedeSenha_QuandoSolicitadoPorAdmin` passam retornando `true` sem efeito colateral — a anonimização vem na Task 4.

- [ ] **Step 7: Commit**

```bash
git add Src/Application/DTOs/ExcluirContaDTOs.cs Src/Application/UseCases/Cliente/ExcluirContaCliente.cs Tests/
git commit -m "feat(lgpd): guardas do use case de exclusao de conta"
```

---

## Task 4: `ExcluirContaCliente` — anonimização, deletes e transação

**Repo:** `ProximoTurnoApi`

**Files:**
- Modify: `Src/Application/UseCases/Cliente/ExcluirContaCliente.cs`
- Modify: `Tests/Domain/ExcluirContaClienteTests.cs`

**Interfaces:**
- Consumes: tudo da Task 3
- Produces: nenhuma assinatura nova — `ExecuteAsync` passa a ter efeito

- [ ] **Step 1: Escrever os testes de efeito (vão falhar)**

Acrescente a `Tests/Domain/ExcluirContaClienteTests.cs`:

```csharp
    [Fact]
    public async Task Anonimiza_TodosOsCamposPessoais() {
        var cliente = NovoCliente(7);
        var (useCase, _, _, _, _, _) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(7, "senha", solicitadoPorAdmin: false);

        Assert.True(ok);
        Assert.Equal("cliente removido", cliente.Nome);
        Assert.Equal("anon-7@removido.local", cliente.Email);
        Assert.Equal("anon7", cliente.Telefone);
        Assert.Equal("removido", cliente.Endereco);
        Assert.Null(cliente.Cpf);
        Assert.Null(cliente.DataNascimento);
        Assert.Null(cliente.ComoNosConheceu);
        Assert.False(cliente.AceitaReceberOfertas);
        Assert.False(cliente.Ativo);
        Assert.NotNull(cliente.DataAnonimizacao);
    }

    [Fact]
    public async Task GeraTokensUnicos_ParaClientesDiferentes() {
        var a = NovoCliente(10);
        var b = NovoCliente(11);
        b.Email = "b@x.com";
        b.Telefone = "11888887777";
        b.Cpf = "98765432100";

        var (useCaseA, _, _, _, _, _) = Montar(a);
        await useCaseA.ExecuteAsync(10, "senha", solicitadoPorAdmin: false);

        var (useCaseB, _, _, _, _, _) = Montar(b);
        await useCaseB.ExecuteAsync(11, "senha", solicitadoPorAdmin: false);

        Assert.NotEqual(a.Email, b.Email);
        Assert.NotEqual(a.Telefone, b.Telefone);
    }

    [Fact]
    public async Task Apaga_ComentariosListaDesejosEContratos() {
        var (useCase, clientes, _, contratos, _, _) = Montar(NovoCliente(3));

        await useCase.ExecuteAsync(3, "senha", solicitadoPorAdmin: false);

        Assert.Contains(3, clientes.DadosVinculadosExcluidos);
        Assert.Contains(3, contratos.ContratosExcluidosPorCliente);
    }

    [Fact]
    public async Task Deleta_UsuarioDoIdentity() {
        var (useCase, _, _, _, users, _) = Montar(NovoCliente(4));

        await useCase.ExecuteAsync(4, "senha", solicitadoPorAdmin: false);

        Assert.Contains("u1", users.Deletados);
    }

    [Fact]
    public async Task EnviaEmailDeConfirmacao_ParaOEnderecoReal() {
        var (useCase, _, _, _, _, email) = Montar(NovoCliente(5));

        await useCase.ExecuteAsync(5, "senha", solicitadoPorAdmin: false);

        Assert.Single(email.Enviados);
        Assert.Equal("ana@x.com", email.Enviados[0].to);
    }

    [Fact]
    public async Task FalhaNoEmail_NaoDesfazAExclusao() {
        var cliente = NovoCliente(6);
        var (useCase, _, _, _, _, email) = Montar(cliente);
        email.LancarErro = true;

        var ok = await useCase.ExecuteAsync(6, "senha", solicitadoPorAdmin: false);

        Assert.True(ok);
        Assert.NotNull(cliente.DataAnonimizacao);
    }
```

O `FakeEmailService` com a flag `LancarErro` e a tupla `(to, subject, body)` já foi criado na Task 3, dentro de `ExcluirContaClienteTests`. Nada a acrescentar.

- [ ] **Step 2: Rodar para confirmar que falham**

```bash
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~ExcluirContaCliente"
```

Esperado: os testes novos FAIL (nada é anonimizado ainda); os da Task 3 continuam PASS.

- [ ] **Step 3: Implementar o efeito**

Em `ExcluirContaCliente.cs`, substitua o `return true;` final do `ExecuteAsync` por:

```csharp
        // Capturado antes de anonimizar: o e-mail de confirmação vai para o endereço real.
        var emailReal = cliente.Email;
        var nomeReal = cliente.Nome;

        await clienteRepository.StartTransactionAsync();
        try {
            // Todos os repositórios compartilham o mesmo DatabaseContext scoped, e o Identity
            // usa esse mesmo contexto (AddEntityFrameworkStores<DatabaseContext>). Por isso uma
            // transação aberta aqui cobre também os ExecuteDeleteAsync dos outros repositórios
            // e o DeleteAsync do UserManager — não existe conta meio-excluída.
            await clienteRepository.ExcluirDadosVinculadosAsync(idCliente);
            await contratoRepository.ExcluirPorClienteAsync(idCliente);

            Anonimizar(cliente);
            await clienteRepository.UpdateAsync(cliente);

            if (usuarioCliente is not null) {
                var resultado = await userManager.DeleteAsync(usuarioCliente);
                if (!resultado.Succeeded) {
                    foreach (var erro in resultado.Errors) {
                        AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, erro.Description));
                    }
                    await clienteRepository.RollbackTransactionAsync();
                    return false;
                }
            }

            await clienteRepository.CommitTransactionAsync();
        } catch (Exception ex) {
            await clienteRepository.RollbackTransactionAsync();
            logger.LogError(ex, "Erro ao excluir a conta do cliente {ClienteId}.", idCliente);
            throw;
        }

        logger.LogInformation("Conta do cliente {ClienteId} excluída e anonimizada.", idCliente);

        // Fora da transação de propósito: a exclusão já está feita e é a prioridade.
        // Falha de SMTP vira log, nunca rollback.
        try {
            await emailService.SendEmailAsync(
                emailReal,
                "Sua conta no Próximo Turno foi excluída",
                $"Olá, {nomeReal}.<br><br>Sua conta foi excluída e seus dados pessoais foram removidos " +
                "do nosso sistema. Seu histórico de pedidos foi mantido de forma anônima, como exige " +
                "a legislação fiscal.<br><br>Se não foi você quem pediu, entre em contato conosco.");
        } catch (Exception ex) {
            logger.LogError(ex, "Conta {ClienteId} excluída, mas o e-mail de confirmação falhou.", idCliente);
        }

        return true;
    }

    private static void Anonimizar(Cliente cliente) {
        // Tokens com o id embutido são obrigatórios: EMAIL, TELEFONE e CPF são índices UNIQUE.
        cliente.Nome = "cliente removido";
        cliente.Email = $"anon-{cliente.Id}@removido.local";
        cliente.Telefone = $"anon{cliente.Id}";
        cliente.Endereco = "removido";
        cliente.Cpf = null;
        cliente.DataNascimento = null;
        cliente.ComoNosConheceu = null;
        cliente.AceitaReceberOfertas = false;
        cliente.Ativo = false;
        cliente.DataAnonimizacao = DateTime.Now;
    }
```

- [ ] **Step 4: Rodar a suíte completa**

```bash
dotnet test Tests/Tests.csproj
```

Esperado: todos PASS, incluindo os testes pré-existentes.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Cliente/ExcluirContaCliente.cs Tests/
git commit -m "feat(lgpd): anonimizacao e expurgo na exclusao de conta"
```

---

## Task 5: Endpoint `DELETE /api/clientes/{id}/conta`

**Repo:** `ProximoTurnoApi`

**Files:**
- Modify: `Src/Application/Controllers/ClientesController.cs`
- Modify: `Src/Program.cs`

**Interfaces:**
- Consumes: `ExcluirContaCliente.ExecuteAsync`, `ExcluirContaCliente.PedidosEmAberto`, `ExcluirContaRequestDTO`
- Produces: `DELETE /api/clientes/{id}/conta`

- [ ] **Step 1: Registrar o use case**

Em `Src/Program.cs`, ao lado de `builder.Services.AddScoped<AtualizarStatusCliente>();` (linha ~106):

```csharp
builder.Services.AddScoped<ExcluirContaCliente>();
```

- [ ] **Step 2: Injetar no controller**

Em `Src/Application/Controllers/ClientesController.cs`, acrescente ao construtor primário, antes de `UserManager<Usuario> _userManager`:

```csharp
                            ExcluirContaCliente _excluirContaClienteUseCase,
```

- [ ] **Step 3: Adicionar o endpoint**

No fim de `ClientesController`, antes do fechamento da classe:

```csharp
    /// <summary>
    /// Exclusão de conta pelo titular (LGPD Art. 18, VI). O próprio cliente precisa informar a
    /// senha; um Admin pode executar sem senha para atender pedidos vindos por outros canais.
    /// </summary>
    [HttpDelete("{id:int}/conta")]
    [Authorize]
    public async Task<IActionResult> ExcluirConta([FromRoute] int id, [FromBody] ExcluirContaRequestDTO? request) {
        return await EncapsulateRequestAsync(async () => {
            var usuarioLogado = await _userManager.GetUserAsync(User);
            if (usuarioLogado is null) {
                return Unauthorized();
            }

            var isAdmin = await _userManager.IsInRoleAsync(usuarioLogado, Roles.Admin);
            var idClienteLogado = await _repository.GetIdByEmailAsync(usuarioLogado.Email ?? "");
            if (!isAdmin && idClienteLogado != id) {
                _logger.LogWarning("Usuário logado tentou excluir a conta de outro cliente.");
                return Forbid();
            }

            var sucesso = await _excluirContaClienteUseCase.ExecuteAsync(id, request?.Senha, isAdmin);
            if (sucesso) {
                return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Conta excluída com sucesso."));
            }

            // Pedidos em aberto viram 409 para o frontend montar uma tela própria, em vez de
            // um 400 indistinguível de "senha incorreta".
            if (_excluirContaClienteUseCase.PedidosEmAberto.Count > 0) {
                // Montado na mão porque isto é uma falha que carrega dados: CreateFailureResult
                // zera o Data e CreateSuccessResult marcaria Success = true.
                return Conflict(new ApiResultDTO<List<PedidoEmAbertoDTO>> {
                    Success = false,
                    Message = _excluirContaClienteUseCase.AggregateErrors(),
                    Data = [.. _excluirContaClienteUseCase.PedidosEmAberto]
                });
            }

            var notification = _excluirContaClienteUseCase.Notifications.FirstOrDefault();
            if (notification?.Type == UseCaseNotificationType.NotFound) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult(_excluirContaClienteUseCase.AggregateErrors()));
            }

            return BadRequest(ApiResultDTO<object>.CreateFailureResult(_excluirContaClienteUseCase.AggregateErrors()));
        });
    }
```

`ApiResultDTO<T>` expõe `Success`, `Message` e `Data` públicos (ver `Src/Application/DTOs/ApiResultDTO.cs`), então o inicializador acima compila sem helper novo.

- [ ] **Step 4: Compilar**

```bash
dotnet build
```

Esperado: `Build succeeded`.

- [ ] **Step 5: Verificar manualmente que o corpo do DELETE chega**

Suba a API (`docker-compose up -d` ou `dotnet run --project Src/ProximoTurnoApi.csproj`) e chame o endpoint com um token de cliente e senha errada:

```bash
curl -i -X DELETE http://localhost:5016/api/clientes/1/conta \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"senha":"errada"}'
```

Esperado: `400` com "Senha incorreta." — o que prova que o corpo chegou. Se vier "Senha incorreta." mesmo com a senha certa, o corpo está sendo descartado: troque o verbo para `[HttpPost("{id:int}/excluir-conta")]` e ajuste o frontend na Task 7 para `POST`.

- [ ] **Step 6: Commit**

```bash
git add Src/Application/Controllers/ClientesController.cs Src/Program.cs
git commit -m "feat(lgpd): endpoint DELETE /api/clientes/{id}/conta"
```

---

## Task 6: Varredura das rotas

**Repo:** `ProximoTurnoApi`

**Files:**
- Modify: `Src/Application/UseCases/Cliente/EnviarEmailsClientes.cs`
- Modify: `Src/Infrastructure/Repositories/ClienteRepository.cs`
- Modify: `Src/Application/UseCases/Comentario/ObterComentariosJogo.cs`
- Modify: `Src/Application/Controllers/ClientesController.cs`
- Modify: `Tests/Domain/EnviarEmailsClientesTests.cs`

**Interfaces:**
- Consumes: `Cliente.DataAnonimizacao`
- Produces: nenhuma assinatura nova

- [ ] **Step 1: Teste de que e-mail em massa pula anonimizado (vai falhar)**

Em `Tests/Domain/EnviarEmailsClientesTests.cs`:

```csharp
    [Fact]
    public async Task NaoEnvia_ParaClienteAnonimizado() {
        var repo = new FakeClienteRepository {
            Clientes = {
                new Cliente {
                    Id = 1, Nome = "cliente removido", Email = "anon-1@removido.local",
                    Telefone = "anon1", Endereco = "removido",
                    Ativo = false, DataAnonimizacao = new DateTime(2026, 1, 1)
                }
            }
        };
        var email = new FakeEmailService();
        var links = new FakeResetSenhaLinkService("https://site/r", "https://site/a");
        var useCase = new EnviarEmailsClientes(repo, email, links, NullLogger<EnviarEmailsClientes>.Instance);

        await useCase.ExecuteAsync(new EnviarEmailsClientesRequest {
            ClienteIds = [1], Titulo = "Oi", Conteudo = "Promoção"
        });

        Assert.Empty(email.Enviados);
    }
```

- [ ] **Step 2: Rodar e confirmar a falha**

```bash
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~EnviarEmailsClientes"
```

Esperado: `NaoEnvia_ParaClienteAnonimizado` FAIL — um e-mail foi enviado para `anon-1@removido.local`.

- [ ] **Step 3: Filtrar no repositório**

Em `ClienteRepository.GetAllByIdsAsync`:

```csharp
    public async Task<List<Cliente>> GetAllByIdsAsync(List<int> ids) {
        return await _dbContext.Clientes
            .AsNoTracking()
            // Contas excluídas pelo titular nunca entram em disparo de e-mail: o endereço é um
            // token sintético e o consentimento de marketing foi revogado junto com a conta.
            .Where(c => ids.Contains(c.Id) && c.DataAnonimizacao == null)
            .ToListAsync();
    }
```

⚠️ **O teste do Step 1 usa o `FakeClienteRepository` privado de `EnviarEmailsClientesTests.cs:114`, não o compartilhado.** É lá que o filtro precisa entrar para o teste ficar verde. Ajuste **os dois**, para os fakes não divergirem:

Em `Tests/Domain/EnviarEmailsClientesTests.cs`, no `FakeClienteRepository` privado:

```csharp
        public Task<List<Cliente>> GetAllByIdsAsync(List<int> ids) =>
            Task.FromResult(Clientes.Where(c => ids.Contains(c.Id) && c.DataAnonimizacao == null).ToList());
```

E em `Tests/Fakes/PedidoUseCaseFakes.cs`, no compartilhado:

```csharp
    public Task<List<Cliente>> GetAllByIdsAsync(List<int> ids) =>
        Task.FromResult(Clientes.Where(c => ids.Contains(c.Id) && c.DataAnonimizacao == null).ToList());
```

Confira a assinatura exata da cópia privada antes de colar — a indentação e o corpo podem diferir.

- [ ] **Step 4: Rodar e confirmar que passa**

```bash
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~EnviarEmailsClientes"
```

Esperado: todos PASS.

- [ ] **Step 5: Filtrar comentários públicos**

Em `Src/Application/UseCases/Comentario/ObterComentariosJogo.cs`, no `Where`:

```csharp
            .Where(c => c.IdJogo == jogoId
                     && c.Status == StatusComentario.Aprovado
                     // Cinto de segurança: os comentários do cliente já são apagados na exclusão
                     // de conta, este filtro protege contra linhas órfãs de dados legados.
                     && c.Cliente.DataAnonimizacao == null)
```

- [ ] **Step 6: Recusar perfil de conta excluída**

Em `ClientesController.GetPerfilCliente`, logo após a checagem `if (cliente == null)`:

```csharp
            if (cliente.DataAnonimizacao is not null) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult($"Cliente de id {id} não encontrado."));
            }
```

E em `AtualizarCliente` (`Src/Application/UseCases/Cliente/AtualizarCliente.cs`), logo após carregar o cliente e antes de qualquer alteração:

```csharp
        if (clienteExistente.DataAnonimizacao is not null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest, "Não é possível alterar uma conta excluída."));
            return false;
        }
```

Nota: confira o nome real da variável do cliente carregado em `AtualizarCliente.cs` antes de colar.

- [ ] **Step 7: Suíte completa**

```bash
dotnet build && dotnet test Tests/Tests.csproj
```

Esperado: todos PASS.

- [ ] **Step 8: Commit**

```bash
git add Src/ Tests/
git commit -m "feat(lgpd): ignora contas excluidas nas rotas de cliente"
```

---

## Task 7: Frontend — erro tipado e chamada da API

**Repo:** `ProximoTurno` (frontend — repo diferente!)

**Files:**
- Modify: `lib/api-service.ts:281-311` (método `request`) e área dos métodos de cliente (~linha 955)
- Create: `app/api/clientes/[id]/conta/route.ts`

**Interfaces:**
- Consumes: `DELETE /api/clientes/{id}/conta` (Task 5)
- Produces:
  - `export class ApiRequestError extends Error { status: number; body: string }`
  - `apiService.excluirConta(id: number, senha?: string): Promise<any>`

- [ ] **Step 1: Criar a branch**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurno
git rev-parse --show-toplevel   # precisa imprimir .../ProximoTurno/ProximoTurno
git checkout -b feat/exclusao-conta-lgpd
```

- [ ] **Step 2: Adicionar `ApiRequestError`**

Em `lib/api-service.ts`, acima da classe do serviço:

```typescript
/**
 * Erro de requisição que preserva o status HTTP. A mensagem mantém exatamente o formato
 * anterior para que getErrorMessage() e todos os catch existentes sigam funcionando.
 */
export class ApiRequestError extends Error {
    constructor(
        message: string,
        public readonly status: number,
        public readonly body: string,
    ) {
        super(message)
        this.name = "ApiRequestError"
    }
}
```

- [ ] **Step 3: Lançar o erro tipado**

Em `lib/api-service.ts`, no bloco `if (!response.ok)` do método `request` (linhas ~308-311), troque:

```typescript
        if (!response.ok) {
            const errorText = await response.text()
            throw new ApiRequestError(
                `Erro na requisição ${endpoint}: ${response.statusText}. ${errorText}`,
                response.status,
                errorText,
            )
        }
```

A string da mensagem tem que ficar **idêntica** à anterior — `getErrorMessage` a parseia procurando o primeiro `{`.

- [ ] **Step 4: Adicionar o método de serviço**

Em `lib/api-service.ts`, logo após `inativarCliente` (~linha 959):

```typescript
    /** Exclusão de conta pelo titular (LGPD). Senha é obrigatória quando é o próprio cliente. */
    async excluirConta(id: number, senha?: string): Promise<any> {
        return await this.request(`/clientes/${id}/conta`, {
            method: "DELETE",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ senha: senha ?? null }),
        })
    }
```

- [ ] **Step 5: Criar o proxy**

`app/api/clientes/[id]/conta/route.ts`:

```typescript
import { type NextRequest, NextResponse } from "next/server"
import { getBaseHeaders } from "@/lib/api-utils"

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5016"

export async function DELETE(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  try {
    const headers = getBaseHeaders(request)
    const { id } = await params
    const body = await request.text()
    const url = `${baseUrl}/api/clientes/${id}/conta`

    const response = await fetch(url, {
      method: "DELETE",
      headers,
      body,
    })

    const data = await response.json()
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    console.error("Erro ao excluir conta:", error)
    return NextResponse.json({ error: "Erro ao excluir conta" }, { status: 500 })
  }
}
```

- [ ] **Step 6: Verificar que compila**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurno
npx tsc --noEmit
```

Esperado: sem erros. Se o projeto não tiver `tsc` disponível assim, rode `npm run build`.

- [ ] **Step 7: Commit**

```bash
git add lib/api-service.ts app/api/clientes/
git commit -m "feat(lgpd): erro tipado com status e chamada de exclusao de conta"
```

---

## Task 8: Frontend — dialog de exclusão no perfil

**Repo:** `ProximoTurno`

**Files:**
- Create: `components/excluir-conta-dialog.tsx`
- Modify: `app/perfil/page.tsx`

**Interfaces:**
- Consumes: `apiService.excluirConta`, `ApiRequestError` (Task 7)
- Produces: `<ExcluirContaDialog idCliente={number} onExcluido={() => void} />`

- [ ] **Step 1: Conferir os componentes disponíveis**

```bash
ls components/ui/ | grep -i "dialog\|alert\|input\|button"
```

Use os que existirem (o projeto usa Radix). Se houver `alert-dialog`, prefira-o ao `dialog` para ação destrutiva.

- [ ] **Step 2: Criar o componente**

`components/excluir-conta-dialog.tsx`:

```tsx
"use client"

import { useState } from "react"
import { useApiService } from "@/lib/use-api-service"
import { ApiRequestError, getErrorMessage } from "@/lib/api-service"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
    Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from "@/components/ui/dialog"
import { Loader2 } from "lucide-react"
import { toast } from "sonner"

interface PedidoEmAberto {
    id: number
    dataHora: string
    jogos: string[]
}

export function ExcluirContaDialog({ idCliente, onExcluido }: { idCliente: number; onExcluido: () => void }) {
    const apiService = useApiService()
    const [aberto, setAberto] = useState(false)
    const [senha, setSenha] = useState("")
    const [enviando, setEnviando] = useState(false)
    const [pedidosEmAberto, setPedidosEmAberto] = useState<PedidoEmAberto[] | null>(null)

    const fechar = (v: boolean) => {
        setAberto(v)
        if (!v) {
            setSenha("")
            setPedidosEmAberto(null)
        }
    }

    const excluir = async () => {
        setEnviando(true)
        try {
            await apiService.excluirConta(idCliente, senha)
            toast.success("Sua conta foi excluída.")
            onExcluido()
        } catch (error) {
            // 409 é o único caso que ganha tela própria: o cliente precisa ver quais jogos devolver.
            if (error instanceof ApiRequestError && error.status === 409) {
                try {
                    setPedidosEmAberto(JSON.parse(error.body).data ?? [])
                } catch {
                    setPedidosEmAberto([])
                }
                return
            }
            toast.error(getErrorMessage(error, "Não foi possível excluir a conta."))
        } finally {
            setEnviando(false)
        }
    }

    return (
        <Dialog open={aberto} onOpenChange={fechar}>
            <DialogTrigger asChild>
                <Button variant="destructive">Excluir minha conta</Button>
            </DialogTrigger>
            <DialogContent>
                {pedidosEmAberto ? (
                    <>
                        <DialogHeader>
                            <DialogTitle>Você tem pedidos em aberto</DialogTitle>
                            <DialogDescription>
                                Devolva os jogos abaixo para poder excluir sua conta.
                            </DialogDescription>
                        </DialogHeader>
                        <ul className="space-y-2 text-sm">
                            {pedidosEmAberto.map((p) => (
                                <li key={p.id}>
                                    <span className="font-medium">Pedido #{p.id}</span> — {p.jogos.join(", ")}
                                </li>
                            ))}
                        </ul>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => fechar(false)}>Fechar</Button>
                        </DialogFooter>
                    </>
                ) : (
                    <>
                        <DialogHeader>
                            <DialogTitle>Excluir sua conta</DialogTitle>
                            <DialogDescription>
                                Esta ação não pode ser desfeita. Seus dados pessoais, comentários e lista de
                                desejos serão removidos, e seu login deixará de existir. Seu histórico de
                                pedidos é mantido de forma anônima, como exige a legislação fiscal.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="space-y-2">
                            <label htmlFor="senha-exclusao" className="text-sm font-medium">
                                Confirme sua senha
                            </label>
                            <Input
                                id="senha-exclusao"
                                type="password"
                                value={senha}
                                onChange={(e) => setSenha(e.target.value)}
                                autoComplete="current-password"
                            />
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => fechar(false)}>Cancelar</Button>
                            <Button variant="destructive" onClick={excluir} disabled={enviando || !senha}>
                                {enviando && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                Excluir definitivamente
                            </Button>
                        </DialogFooter>
                    </>
                )}
            </DialogContent>
        </Dialog>
    )
}
```

Nota: confira em `lib/api-utils.ts` / `lib/api-service.ts` se a resposta do backend serializa como `data` (camelCase). O `ApiResultDTO` do backend expõe `Data`; com a config padrão de JSON do ASP.NET Core chega como `data`. Se chegar diferente, ajuste o `JSON.parse(error.body).data`.

- [ ] **Step 3: Ligar no perfil**

Em `app/perfil/page.tsx`, importe:

```tsx
import { ExcluirContaDialog } from "@/components/excluir-conta-dialog"
import { useAuth } from "@/lib/auth-context"
```

(`useAuth` já está importado.) Extraia `logout` do contexto — confira o nome exato exportado por `lib/auth-context.tsx`:

```tsx
    const { usuario, isLoading, updateUsuario, logout } = useAuth()
```

E, ao final do `<CardContent>` que já renderiza o `ClientForm`, acrescente:

```tsx
                    {usuario?.idCliente && (
                        <div className="mt-10 rounded-lg border border-destructive/40 p-4">
                            <h3 className="text-sm font-semibold text-destructive">Zona de risco</h3>
                            <p className="mb-4 mt-1 text-sm text-muted-foreground">
                                Excluir sua conta remove seus dados pessoais de forma permanente.
                            </p>
                            <ExcluirContaDialog
                                idCliente={usuario.idCliente}
                                onExcluido={() => {
                                    logout()
                                    router.push("/")
                                }}
                            />
                        </div>
                    )}
```

- [ ] **Step 4: Verificar que compila**

```bash
npx tsc --noEmit
```

Esperado: sem erros.

- [ ] **Step 5: Verificação manual**

Suba backend e frontend, entre com um cliente de teste e confira os quatro caminhos:

1. Senha errada → toast de erro, dialog segue aberto.
2. Cliente com pedido `Entregue` → tela com a lista de jogos a devolver.
3. Cliente sem pedido em aberto → exclusão, logout, redirect para `/`.
4. Tentar logar de novo com o mesmo e-mail → falha (o `IdentityUser` não existe mais).

Confira no banco que `CLIENTE` do id ficou com `NOME = 'cliente removido'`, `DATA_ANONIMIZACAO` preenchida, e que `COMENTARIO`/`LISTA_DESEJOS` daquele cliente estão vazios.

- [ ] **Step 6: Commit**

```bash
git add components/excluir-conta-dialog.tsx app/perfil/page.tsx
git commit -m "feat(lgpd): dialog de exclusao de conta no perfil"
```

---

## Task 9: Admin — atender pedido de exclusão vindo por outro canal

**Repo:** `ProximoTurno`

**Files:**
- Modify: `lib/api-service.ts` (interface `Cliente`)
- Modify: `app/admin/clientes/page.tsx:213-229` (ao lado de `handleDelete`)

**Interfaces:**
- Consumes: `apiService.excluirConta` (Task 7); `ClienteDTO.DataAnonimizacao` (Task 1)
- Produces: nenhuma assinatura nova

- [ ] **Step 1: Expor o campo no tipo do frontend**

Em `lib/api-service.ts`, na interface `Cliente`, logo após `loginAtivo`:

```typescript
    /** Somente leitura: quando o titular excluiu a conta. Null/undefined = conta ativa. */
    dataAnonimizacao?: string | null
```

- [ ] **Step 2: Adicionar o handler**

Em `app/admin/clientes/page.tsx`, logo após `handleActivate`:

```tsx
    // Atende pedidos de exclusão que chegam por e-mail/WhatsApp, de quem não consegue mais
    // logar. Sem senha: a autenticação de admin já é o controle. Ver decisão 6 da spec —
    // contas com perfil de administrador são recusadas pelo backend.
    const handleExcluirConta = (cliente: Cliente) => {
        setConfirmConfig({
            title: "Excluir conta (LGPD)",
            description: `Excluir permanentemente a conta de "${cliente.nome}"? Os dados pessoais serão anonimizados e os comentários, lista de desejos e login serão apagados. O histórico de pedidos é mantido de forma anônima. Esta ação não pode ser desfeita.`,
            action: async () => {
                try {
                    await apiService.excluirConta(cliente.id!)
                    toast.success("Conta excluída e dados anonimizados")
                    setSelectedIds(prev => { const next = new Set(prev); next.delete(cliente.id!); return next })
                    fetchClientes()
                } catch (error) {
                    toast.error(getErrorMessage(error, "Erro ao excluir conta"))
                }
            }
        })
        setConfirmOpen(true)
    }
```

- [ ] **Step 3: Ligar na linha do grid**

Localize o menu de ações da linha (onde `handleDelete` e `handleActivate` já são chamados) e acrescente um item que só aparece para contas ainda não excluídas:

```tsx
                            {!cliente.dataAnonimizacao && (
                                <DropdownMenuItem
                                    className="text-destructive"
                                    onClick={() => handleExcluirConta(cliente)}
                                >
                                    Excluir conta (LGPD)
                                </DropdownMenuItem>
                            )}
```

Nota: confira o componente real usado no menu daquela tela (`DropdownMenuItem` ou botão direto) e siga o que já existe ali, incluindo o import.

- [ ] **Step 4: Marcar visualmente as contas excluídas**

Ainda na linha do grid, onde o status do cliente é renderizado, acrescente antes do badge de ativo/inativo:

```tsx
                        {cliente.dataAnonimizacao && (
                            <Badge variant="outline" className="text-muted-foreground">
                                Conta excluída
                            </Badge>
                        )}
```

- [ ] **Step 5: Verificar que compila**

```bash
cd /d/Repositorios/ProximoTurno/ProximoTurno
npx tsc --noEmit
```

Esperado: sem erros.

- [ ] **Step 6: Verificação manual**

Com backend e frontend no ar, logado como Admin, na tela `/admin/clientes`:

1. Excluir a conta de um cliente sem pedidos em aberto → toast de sucesso, grid recarrega, linha aparece com "Conta excluída" e nome "cliente removido".
2. Tentar excluir um cliente com pedido `Entregue` → toast de erro com a mensagem de pedidos em aberto.
3. Tentar excluir a própria conta de admin → toast com a mensagem de duas etapas.
4. A ação "Excluir conta (LGPD)" não aparece em linha já excluída.

- [ ] **Step 7: Commit**

```bash
git add lib/api-service.ts app/admin/clientes/page.tsx
git commit -m "feat(lgpd): admin pode atender pedido de exclusao de conta"
```

---

## Fora de escopo deste plano

Registrado na spec, não implemente aqui:

- Expurgo do documento no Autentique (decisão 5 — risco residual assumido)
- Política de retenção com prazo para os pedidos preservados
- Exportação de dados do titular (Art. 18, V)
- Aviso na tela de cadastro e política de privacidade
