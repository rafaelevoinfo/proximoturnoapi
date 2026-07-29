# Backup automatizado para Backblaze B2 — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Backup noturno automático do banco MySQL (cifrado) e do volume de uploads (incremental) para a Backblaze B2, com alerta por e-mail de sucesso e de falha.

**Architecture:** Um `BackgroundService` na própria API cuida apenas do agendamento; toda a orquestração fica num `ExecutarBackup` sem dependência de tempo, que recebe suas colaboradoras por interface e é 100% testável com fakes. O dump usa `mysqldump` como processo filho conectando pela rede ao contêiner MySQL, com a saída em pipe para `gzip` e `gpg`. O envio usa o SDK da AWS apontado para o endpoint S3-compatível da Backblaze.

**Tech Stack:** .NET 10, xUnit, AWSSDK.S3, MailKit (via `IEmailService` existente), `mysqldump` e `gpg` como binários do contêiner.

**Spec:** `docs/superpowers/specs/2026-07-28-backup-automatizado-b2-design.md`

## Global Constraints

- Idioma de logs, e-mails, mensagens de commit e comentários: **português**.
- Namespace raiz: `ProximoTurnoApi`. Testes em `ProximoTurnoApi.Tests.Domain`.
- Não existe biblioteca de mocking no projeto — **todos os dublês são classes fake escritas à mão**, no padrão de `Tests/Domain/ContratoQueueBackgroundServiceTests.cs`.
- Configuração lida via `IConfiguration["NOME_DA_VARIAVEL"]`, como em `Src/Infrastructure/Services/EmailService.cs:19`.
- **O socket do Docker não pode ser montado no contêiner da API.**
- Valores padrão embutidos: `BACKUP_ENABLED=true`, `BACKUP_HORA=03:00`, `BACKUP_EMAIL_DESTINO=contato@proximoturno.com.br`, `B2_ENDPOINT=https://s3.us-east-005.backblazeb2.com`, `B2_BUCKET=proximo-turno`.
- Sem padrão (segredos): `BACKUP_PASSPHRASE`, `B2_KEY_ID`, `B2_APPLICATION_KEY`.
- Retenção é responsabilidade da regra de ciclo de vida de 7 dias do bucket. **A aplicação nunca apaga objetos remotos.**
- Chave do dump no bucket: `db/AAAA-MM-DD.sql.gz.gpg`. Prefixo dos uploads: `uploads/`.

## Estrutura de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `Src/Application/UseCases/Backup/BackupOptions.cs` | Opções + padrões embutidos + verificação de segredos |
| `Src/Application/UseCases/Backup/IEstadoBackupStore.cs` | Contrato de persistência do estado |
| `Src/Application/UseCases/Backup/EstadoBackupArquivo.cs` | Estado em JSON num volume dedicado |
| `Src/Application/UseCases/Backup/IArmazenamentoBackup.cs` | Contrato de armazenamento remoto |
| `Src/Infrastructure/Backup/ArmazenamentoB2.cs` | Implementação com AWSSDK.S3 |
| `Src/Application/UseCases/Backup/IDumpBanco.cs` | Contrato do dump |
| `Src/Infrastructure/Backup/DumpBancoMySql.cs` | `mysqldump \| gzip \| gpg` |
| `Src/Application/UseCases/Backup/ISincronizadorUploads.cs` | Contrato da sincronização |
| `Src/Infrastructure/Backup/SincronizadorUploads.cs` | Diferença local × remoto |
| `Src/Application/UseCases/Backup/ExecutarBackup.cs` | **Orquestração — o núcleo testável** |
| `Src/Application/UseCases/Backup/BackupBackgroundService.cs` | Apenas agendamento |
| `Tests/Domain/Backup*Tests.cs` | Testes |
| `docs/RESTORE.md` | Runbook de restauração |

As interfaces ficam em `Application/UseCases/Backup` junto de quem as consome; as implementações que tocam processo, disco e rede ficam em `Infrastructure/Backup`. Isso permite testar a orquestração sem MySQL, sem Backblaze e sem SMTP.

---

### Task 1: `BackupOptions` — configuração e padrões

**Files:**
- Create: `Src/Application/UseCases/Backup/BackupOptions.cs`
- Test: `Tests/Domain/BackupOptionsTests.cs`

**Interfaces:**
- Consumes: nada
- Produces: `BackupOptions` com propriedades `Habilitado: bool`, `Horario: TimeSpan`, `EmailDestino: string`, `B2Endpoint: string`, `B2Bucket: string`, `Passphrase: string?`, `B2KeyId: string?`, `B2ApplicationKey: string?`, `CaminhoUploads: string`, `CaminhoEstado: string`, propriedade `SegredosPresentes: bool` e método estático `DaConfiguracao(IConfiguration): BackupOptions`

- [ ] **Step 1: Escrever os testes que falham**

Crie `Tests/Domain/BackupOptionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BackupOptionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    [Fact]
    public void DaConfiguracao_SemVariaveis_UsaPadroesEmbutidos()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()));

        Assert.True(options.Habilitado);
        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
        Assert.Equal("contato@proximoturno.com.br", options.EmailDestino);
        Assert.Equal("https://s3.us-east-005.backblazeb2.com", options.B2Endpoint);
        Assert.Equal("proximo-turno", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_ComVariaveis_SobrescreveOsPadroes()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_ENABLED"] = "false",
            ["BACKUP_HORA"] = "04:30",
            ["B2_BUCKET"] = "outro-bucket"
        }));

        Assert.False(options.Habilitado);
        Assert.Equal(new TimeSpan(4, 30, 0), options.Horario);
        Assert.Equal("outro-bucket", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_HoraInvalida_CaiParaPadrao()
    {
        var options = BackupOptions.DaConfiguracao(Config(new() { ["BACKUP_HORA"] = "banana" }));

        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
    }

    [Fact]
    public void SegredosPresentes_SemSegredos_RetornaFalso()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()));

        Assert.False(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_ComOsTresSegredos_RetornaVerdadeiro()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id",
            ["B2_APPLICATION_KEY"] = "chave"
        }));

        Assert.True(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_FaltandoUmSegredo_RetornaFalso()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id"
        }));

        Assert.False(options.SegredosPresentes);
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~BackupOptionsTests"`
Expected: FAIL na compilação — `BackupOptions` não existe.

- [ ] **Step 3: Implementar `BackupOptions`**

Crie `Src/Application/UseCases/Backup/BackupOptions.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Opções do backup automatizado. Todos os valores não sensíveis têm padrão
/// embutido; apenas os três segredos precisam vir do ambiente.
/// </summary>
public class BackupOptions
{
    public bool Habilitado { get; init; } = true;
    public TimeSpan Horario { get; init; } = new(3, 0, 0);
    public string EmailDestino { get; init; } = "contato@proximoturno.com.br";
    public string B2Endpoint { get; init; } = "https://s3.us-east-005.backblazeb2.com";
    public string B2Bucket { get; init; } = "proximo-turno";
    public string CaminhoUploads { get; init; } = "/app/wwwroot/uploads";
    public string CaminhoEstado { get; init; } = "/app/backup-state/ultimo-backup.json";

    public string? Passphrase { get; init; }
    public string? B2KeyId { get; init; }
    public string? B2ApplicationKey { get; init; }

    /// <summary>
    /// Sem os três segredos o backup não tem como cifrar nem enviar, então o
    /// serviço nem chega a agendar execuções.
    /// </summary>
    public bool SegredosPresentes =>
        !string.IsNullOrWhiteSpace(Passphrase) &&
        !string.IsNullOrWhiteSpace(B2KeyId) &&
        !string.IsNullOrWhiteSpace(B2ApplicationKey);

    public static BackupOptions DaConfiguracao(IConfiguration configuration)
    {
        var padrao = new BackupOptions();

        return new BackupOptions
        {
            Habilitado = LerBool(configuration["BACKUP_ENABLED"], padrao.Habilitado),
            Horario = LerHorario(configuration["BACKUP_HORA"], padrao.Horario),
            EmailDestino = LerTexto(configuration["BACKUP_EMAIL_DESTINO"], padrao.EmailDestino),
            B2Endpoint = LerTexto(configuration["B2_ENDPOINT"], padrao.B2Endpoint),
            B2Bucket = LerTexto(configuration["B2_BUCKET"], padrao.B2Bucket),
            CaminhoUploads = LerTexto(configuration["BACKUP_CAMINHO_UPLOADS"], padrao.CaminhoUploads),
            CaminhoEstado = LerTexto(configuration["BACKUP_CAMINHO_ESTADO"], padrao.CaminhoEstado),
            Passphrase = configuration["BACKUP_PASSPHRASE"],
            B2KeyId = configuration["B2_KEY_ID"],
            B2ApplicationKey = configuration["B2_APPLICATION_KEY"]
        };
    }

    private static string LerTexto(string? valor, string padrao) =>
        string.IsNullOrWhiteSpace(valor) ? padrao : valor;

    private static bool LerBool(string? valor, bool padrao) =>
        bool.TryParse(valor, out var resultado) ? resultado : padrao;

    private static TimeSpan LerHorario(string? valor, TimeSpan padrao) =>
        TimeSpan.TryParse(valor, out var resultado) ? resultado : padrao;
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~BackupOptionsTests"`
Expected: PASS — 6 testes.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Backup/BackupOptions.cs Tests/Domain/BackupOptionsTests.cs
git commit -m "feat: opcoes de backup com valores padrao embutidos"
```

---

### Task 2: Estado da última execução

**Files:**
- Create: `Src/Application/UseCases/Backup/IEstadoBackupStore.cs`
- Create: `Src/Application/UseCases/Backup/EstadoBackupArquivo.cs`
- Test: `Tests/Domain/EstadoBackupArquivoTests.cs`

**Interfaces:**
- Consumes: `BackupOptions.CaminhoEstado` (Task 1)
- Produces:
  - `record EstadoBackup(DateTime UltimaExecucaoUtc, long TamanhoDumpBytes)`
  - `interface IEstadoBackupStore` com `Task<EstadoBackup?> LerAsync()` e `Task GravarAsync(EstadoBackup estado)`
  - `class EstadoBackupArquivo(string caminho) : IEstadoBackupStore`

- [ ] **Step 1: Escrever os testes que falham**

Crie `Tests/Domain/EstadoBackupArquivoTests.cs`:

```csharp
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EstadoBackupArquivoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"backup-teste-{Guid.NewGuid()}");

    private string Caminho => Path.Combine(_pasta, "ultimo-backup.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public async Task LerAsync_ArquivoInexistente_RetornaNulo()
    {
        var store = new EstadoBackupArquivo(Caminho);

        Assert.Null(await store.LerAsync());
    }

    [Fact]
    public async Task GravarAsync_DepoisLerAsync_DevolveOsMesmosValores()
    {
        var store = new EstadoBackupArquivo(Caminho);
        var momento = new DateTime(2026, 7, 28, 3, 0, 0, DateTimeKind.Utc);

        await store.GravarAsync(new EstadoBackup(momento, 12345));
        var lido = await store.LerAsync();

        Assert.NotNull(lido);
        Assert.Equal(momento, lido!.UltimaExecucaoUtc);
        Assert.Equal(12345, lido.TamanhoDumpBytes);
    }

    [Fact]
    public async Task GravarAsync_CriaODiretorioQuandoNaoExiste()
    {
        var store = new EstadoBackupArquivo(Caminho);

        await store.GravarAsync(new EstadoBackup(DateTime.UtcNow, 1));

        Assert.True(File.Exists(Caminho));
    }

    [Fact]
    public async Task LerAsync_ArquivoCorrompido_RetornaNulo()
    {
        Directory.CreateDirectory(_pasta);
        await File.WriteAllTextAsync(Caminho, "isto nao e json");
        var store = new EstadoBackupArquivo(Caminho);

        Assert.Null(await store.LerAsync());
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~EstadoBackupArquivoTests"`
Expected: FAIL na compilação — `EstadoBackupArquivo` não existe.

- [ ] **Step 3: Implementar**

Crie `Src/Application/UseCases/Backup/IEstadoBackupStore.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>Resultado da última execução bem-sucedida.</summary>
public record EstadoBackup(DateTime UltimaExecucaoUtc, long TamanhoDumpBytes);

public interface IEstadoBackupStore
{
    Task<EstadoBackup?> LerAsync();
    Task GravarAsync(EstadoBackup estado);
}
```

Crie `Src/Application/UseCases/Backup/EstadoBackupArquivo.cs`:

```csharp
using System.Text.Json;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Guarda o estado num JSON dentro de um volume dedicado, para sobreviver a
/// reinícios do contêiner.
/// </summary>
public class EstadoBackupArquivo(string caminho) : IEstadoBackupStore
{
    public async Task<EstadoBackup?> LerAsync()
    {
        if (!File.Exists(caminho)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(caminho);
            return JsonSerializer.Deserialize<EstadoBackup>(json);
        }
        catch (JsonException)
        {
            // Arquivo corrompido não pode impedir o backup de rodar: tratamos
            // como "nunca executou" e a verificação de tamanho é ignorada.
            return null;
        }
    }

    public async Task GravarAsync(EstadoBackup estado)
    {
        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);

        await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(estado));
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~EstadoBackupArquivoTests"`
Expected: PASS — 4 testes.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Backup/IEstadoBackupStore.cs Src/Application/UseCases/Backup/EstadoBackupArquivo.cs Tests/Domain/EstadoBackupArquivoTests.cs
git commit -m "feat: persistencia do estado da ultima execucao de backup"
```

---

### Task 3: Contratos de dump, armazenamento e sincronização

Esta task cria apenas as **interfaces**, para que a orquestração (Task 4) possa ser escrita e testada antes das implementações que tocam processo e rede. Não há teste próprio — são declarações sem comportamento; a Task 4 as exercita através de fakes.

**Files:**
- Create: `Src/Application/UseCases/Backup/IDumpBanco.cs`
- Create: `Src/Application/UseCases/Backup/IArmazenamentoBackup.cs`
- Create: `Src/Application/UseCases/Backup/ISincronizadorUploads.cs`

**Interfaces:**
- Consumes: nada
- Produces:
  - `record ResultadoDump(string CaminhoArquivo, long TamanhoBytes)`
  - `interface IDumpBanco` com `Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken)`
  - `interface IArmazenamentoBackup` com `Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)` e `Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)`
  - `interface ISincronizadorUploads` com `Task<int> SincronizarAsync(CancellationToken cancellationToken)`

- [ ] **Step 1: Criar os três arquivos de contrato**

Crie `Src/Application/UseCases/Backup/IDumpBanco.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>Arquivo de dump já comprimido e cifrado, pronto para envio.</summary>
public record ResultadoDump(string CaminhoArquivo, long TamanhoBytes);

public interface IDumpBanco
{
    Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken);
}
```

Crie `Src/Application/UseCases/Backup/IArmazenamentoBackup.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.Backup;

public interface IArmazenamentoBackup
{
    Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken);

    /// <summary>Chaves já existentes sob um prefixo, sem o prefixo removido.</summary>
    Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken);
}
```

Crie `Src/Application/UseCases/Backup/ISincronizadorUploads.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.Backup;

public interface ISincronizadorUploads
{
    /// <summary>Envia apenas os arquivos ausentes no bucket. Retorna quantos foram enviados.</summary>
    Task<int> SincronizarAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Confirmar que o projeto compila**

Run: `dotnet build Src/ProximoTurnoApi.csproj`
Expected: sucesso, sem erros.

- [ ] **Step 3: Commit**

```bash
git add Src/Application/UseCases/Backup/IDumpBanco.cs Src/Application/UseCases/Backup/IArmazenamentoBackup.cs Src/Application/UseCases/Backup/ISincronizadorUploads.cs
git commit -m "feat: contratos de dump, armazenamento e sincronizacao de backup"
```

---

### Task 4: `ExecutarBackup` — orquestração

O núcleo do sistema. Não toca disco, rede nem processo: tudo entra por interface, então os testes cobrem todos os caminhos sem infraestrutura.

**Files:**
- Create: `Src/Application/UseCases/Backup/ExecutarBackup.cs`
- Test: `Tests/Domain/ExecutarBackupTests.cs`

**Interfaces:**
- Consumes: `BackupOptions` (Task 1), `IEstadoBackupStore` + `EstadoBackup` (Task 2), `IDumpBanco` + `ResultadoDump`, `IArmazenamentoBackup`, `ISincronizadorUploads` (Task 3), `IEmailService` de `ProximoTurnoApi.Infrastructure.Services`
- Produces:
  - `record ResultadoBackup(bool Sucesso, long TamanhoDumpBytes, int UploadsNovos, string? Erro)`
  - `class ExecutarBackup` com construtor `(IDumpBanco, IArmazenamentoBackup, ISincronizadorUploads, IEstadoBackupStore, IEmailService, BackupOptions, ILogger<ExecutarBackup>)` e método `Task<ResultadoBackup> ExecuteAsync(CancellationToken cancellationToken)`

- [ ] **Step 1: Escrever os testes que falham**

Crie `Tests/Domain/ExecutarBackupTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ExecutarBackupTests
{
    private class FakeDumpBanco(long tamanho, Exception? erro = null) : IDumpBanco
    {
        public string CaminhoGerado = Path.Combine(Path.GetTempPath(), $"dump-{Guid.NewGuid()}.sql.gz.gpg");

        public Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken)
        {
            if (erro is not null) throw erro;
            File.WriteAllText(CaminhoGerado, "conteudo");
            return Task.FromResult(new ResultadoDump(CaminhoGerado, tamanho));
        }
    }

    private class FakeArmazenamento : IArmazenamentoBackup
    {
        public readonly List<string> ChavesEnviadas = new();

        public Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)
        {
            ChavesEnviadas.Add(chave);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    }

    private class FakeSincronizador(int enviados) : ISincronizadorUploads
    {
        public Task<int> SincronizarAsync(CancellationToken cancellationToken) => Task.FromResult(enviados);
    }

    private class FakeEstadoStore(EstadoBackup? inicial = null) : IEstadoBackupStore
    {
        public EstadoBackup? Estado = inicial;

        public Task<EstadoBackup?> LerAsync() => Task.FromResult(Estado);

        public Task GravarAsync(EstadoBackup estado)
        {
            Estado = estado;
            return Task.CompletedTask;
        }
    }

    private class FakeEmailService : IEmailService
    {
        public readonly List<(string Destino, string Assunto, string Corpo)> Enviados = new();

        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            Enviados.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private static ExecutarBackup Criar(
        IDumpBanco dump,
        IArmazenamentoBackup armazenamento,
        ISincronizadorUploads sincronizador,
        IEstadoBackupStore estado,
        IEmailService email) =>
        new(dump, armazenamento, sincronizador, estado, email,
            new BackupOptions { EmailDestino = "destino@teste.com" },
            NullLogger<ExecutarBackup>.Instance);

    [Fact]
    public async Task ExecuteAsync_CaminhoFeliz_EnviaDumpUploadsEEmailDeSucesso()
    {
        var dump = new FakeDumpBanco(1000);
        var armazenamento = new FakeArmazenamento();
        var estado = new FakeEstadoStore();
        var email = new FakeEmailService();

        var resultado = await Criar(dump, armazenamento, new FakeSincronizador(3), estado, email)
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1000, resultado.TamanhoDumpBytes);
        Assert.Equal(3, resultado.UploadsNovos);
        Assert.Single(armazenamento.ChavesEnviadas);
        Assert.StartsWith("db/", armazenamento.ChavesEnviadas[0]);
        Assert.EndsWith(".sql.gz.gpg", armazenamento.ChavesEnviadas[0]);
        Assert.Single(email.Enviados);
        Assert.Contains("OK", email.Enviados[0].Assunto);
        Assert.Equal("destino@teste.com", email.Enviados[0].Destino);
    }

    [Fact]
    public async Task ExecuteAsync_CaminhoFeliz_GravaEstadoComTamanhoDoDump()
    {
        var estado = new FakeEstadoStore();

        await Criar(new FakeDumpBanco(1000), new FakeArmazenamento(), new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.NotNull(estado.Estado);
        Assert.Equal(1000, estado.Estado!.TamanhoDumpBytes);
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNoDump_EnviaEmailDeFalhaENaoEnviaArquivo()
    {
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();
        var dump = new FakeDumpBanco(0, new Exception("mysqldump morreu"));

        var resultado = await Criar(dump, armazenamento, new FakeSincronizador(0), new FakeEstadoStore(), email)
            .ExecuteAsync(CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Empty(armazenamento.ChavesEnviadas);
        Assert.Single(email.Enviados);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
        Assert.Contains("mysqldump morreu", email.Enviados[0].Corpo);
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNoDump_NaoAtualizaOEstado()
    {
        var anterior = new EstadoBackup(new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc), 999);
        var estado = new FakeEstadoStore(anterior);
        var dump = new FakeDumpBanco(0, new Exception("falhou"));

        await Criar(dump, new FakeArmazenamento(), new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal(anterior, estado.Estado);
    }

    [Fact]
    public async Task ExecuteAsync_DumpEncolheuMaisDeMetade_AbortaEAlerta()
    {
        var estado = new FakeEstadoStore(new EstadoBackup(DateTime.UtcNow.AddDays(-1), 1000));
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();

        var resultado = await Criar(new FakeDumpBanco(400), armazenamento, new FakeSincronizador(0), estado, email)
            .ExecuteAsync(CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Empty(armazenamento.ChavesEnviadas);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
    }

    [Fact]
    public async Task ExecuteAsync_DumpEncolheuPouco_Prossegue()
    {
        var estado = new FakeEstadoStore(new EstadoBackup(DateTime.UtcNow.AddDays(-1), 1000));
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(new FakeDumpBanco(900), armazenamento, new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task ExecuteAsync_PrimeiraExecucao_IgnoraVerificacaoDeTamanho()
    {
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(new FakeDumpBanco(1), armazenamento, new FakeSincronizador(0), new FakeEstadoStore(), new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task ExecuteAsync_SempreRemoveOArquivoTemporario()
    {
        var dump = new FakeDumpBanco(1000);

        await Criar(dump, new FakeArmazenamento(), new FakeSincronizador(0), new FakeEstadoStore(), new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(dump.CaminhoGerado));
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNaSincronizacaoDeUploads_NaoInvalidaODump()
    {
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();

        var resultado = await Criar(new FakeDumpBanco(1000), armazenamento, new SincronizadorQueFalha(), new FakeEstadoStore(), email)
            .ExecuteAsync(CancellationToken.None);

        // O dump já subiu; a falha dos uploads é reportada mas não descarta o backup do banco.
        Assert.False(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
    }

    private class SincronizadorQueFalha : ISincronizadorUploads
    {
        public Task<int> SincronizarAsync(CancellationToken cancellationToken)
            => throw new Exception("bucket indisponivel");
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~ExecutarBackupTests"`
Expected: FAIL na compilação — `ExecutarBackup` não existe.

- [ ] **Step 3: Implementar `ExecutarBackup`**

Crie `Src/Application/UseCases/Backup/ExecutarBackup.cs`:

```csharp
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases.Backup;

public record ResultadoBackup(bool Sucesso, long TamanhoDumpBytes, int UploadsNovos, string? Erro);

/// <summary>
/// Orquestra uma execução de backup. Não tem dependência de tempo nem de
/// infraestrutura: o agendamento fica no BackgroundService.
/// </summary>
public class ExecutarBackup(
    IDumpBanco dumpBanco,
    IArmazenamentoBackup armazenamento,
    ISincronizadorUploads sincronizadorUploads,
    IEstadoBackupStore estadoStore,
    IEmailService emailService,
    BackupOptions options,
    ILogger<ExecutarBackup> logger)
{
    /// <summary>Abaixo desta fração do dump anterior, consideramos que algo deu errado.</summary>
    private const double FracaoMinimaEmRelacaoAoAnterior = 0.5;

    public async Task<ResultadoBackup> ExecuteAsync(CancellationToken cancellationToken)
    {
        string? caminhoTemporario = null;

        try
        {
            var anterior = await estadoStore.LerAsync();

            logger.LogInformation("Iniciando backup.");

            var dump = await dumpBanco.GerarAsync(cancellationToken);
            caminhoTemporario = dump.CaminhoArquivo;

            VerificarTamanho(dump.TamanhoBytes, anterior);

            var chave = $"db/{DateTime.UtcNow:yyyy-MM-dd}.sql.gz.gpg";
            await armazenamento.EnviarArquivoAsync(chave, dump.CaminhoArquivo, cancellationToken);
            logger.LogInformation("Dump enviado para {Chave} ({Bytes} bytes).", chave, dump.TamanhoBytes);

            var uploadsNovos = await sincronizadorUploads.SincronizarAsync(cancellationToken);
            logger.LogInformation("{Quantidade} uploads novos sincronizados.", uploadsNovos);

            await estadoStore.GravarAsync(new EstadoBackup(DateTime.UtcNow, dump.TamanhoBytes));

            await NotificarSucessoAsync(dump.TamanhoBytes, uploadsNovos);

            return new ResultadoBackup(true, dump.TamanhoBytes, uploadsNovos, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na execução do backup.");
            await NotificarFalhaAsync(ex.Message);
            return new ResultadoBackup(false, 0, 0, ex.Message);
        }
        finally
        {
            RemoverTemporario(caminhoTemporario);
        }
    }

    private static void VerificarTamanho(long tamanhoAtual, EstadoBackup? anterior)
    {
        // Na primeira execução não há com o que comparar.
        if (anterior is null || anterior.TamanhoDumpBytes <= 0) return;

        var minimo = anterior.TamanhoDumpBytes * FracaoMinimaEmRelacaoAoAnterior;
        if (tamanhoAtual >= minimo) return;

        throw new InvalidOperationException(
            $"Dump de {tamanhoAtual} bytes é muito menor que o anterior, de {anterior.TamanhoDumpBytes} bytes. " +
            "Envio abortado por suspeita de dump incompleto.");
    }

    private Task NotificarSucessoAsync(long tamanhoBytes, int uploadsNovos)
    {
        var megabytes = tamanhoBytes / 1024d / 1024d;
        var corpo =
            $"<p>Backup concluído em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.</p>" +
            $"<ul><li>Dump do banco: {megabytes:F1} MB</li>" +
            $"<li>Uploads novos enviados: {uploadsNovos}</li></ul>";

        return emailService.SendEmailAsync(
            options.EmailDestino,
            $"Backup OK — {DateTime.UtcNow:yyyy-MM-dd}",
            corpo);
    }

    private async Task NotificarFalhaAsync(string erro)
    {
        try
        {
            await emailService.SendEmailAsync(
                options.EmailDestino,
                $"Backup FALHOU — {DateTime.UtcNow:yyyy-MM-dd}",
                $"<p>O backup não foi concluído.</p><pre>{erro}</pre>");
        }
        catch (Exception ex)
        {
            // Falha ao avisar sobre falha não pode derrubar o serviço.
            logger.LogError(ex, "Não foi possível enviar o e-mail de falha do backup.");
        }
    }

    private void RemoverTemporario(string? caminho)
    {
        if (caminho is null || !File.Exists(caminho)) return;

        try
        {
            File.Delete(caminho);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível remover o arquivo temporário {Caminho}.", caminho);
        }
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~ExecutarBackupTests"`
Expected: PASS — 9 testes.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Backup/ExecutarBackup.cs Tests/Domain/ExecutarBackupTests.cs
git commit -m "feat: orquestracao do backup com verificacao de tamanho e alerta por e-mail"
```

---

### Task 5: `BackupBackgroundService` — agendamento

**Files:**
- Create: `Src/Application/UseCases/Backup/BackupBackgroundService.cs`
- Test: `Tests/Domain/BackupBackgroundServiceTests.cs`

**Interfaces:**
- Consumes: `BackupOptions` (Task 1), `IEstadoBackupStore` (Task 2), `ExecutarBackup` (Task 4), `IServiceScopeFactory`
- Produces: `class BackupBackgroundService : BackgroundService` com construtor `(IServiceScopeFactory, IEstadoBackupStore, BackupOptions, ILogger<BackupBackgroundService>, TimeProvider?)` e método público `Task<bool> DeveExecutarAgoraAsync()`

`DeveExecutarAgoraAsync` é público para permitir testar a regra de recuperação sem esperar o relógio. `TimeProvider` (nativo do .NET 8+) é injetado para que os testes controlem o "agora".

A exigência do spec de **não haver execuções simultâneas** é atendida por construção, não por trava: o laço é sequencial e cada `ExecutarAsync` é aguardado antes do próximo `Task.Delay`, inclusive a execução de recuperação que roda antes do laço começar.

Reutilize `FakeServiceScopeFactory`, `FakeServiceScope` e `FakeServiceProvider`, que já existem em `Tests/Domain/ContratoQueueBackgroundServiceTests.cs` — são classes públicas no namespace `ProximoTurnoApi.Tests.Domain`, então **não as redeclare**.

- [ ] **Step 1: Escrever os testes que falham**

Crie `Tests/Domain/BackupBackgroundServiceTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BackupBackgroundServiceTests
{
    private class FakeEstadoStore(EstadoBackup? inicial) : IEstadoBackupStore
    {
        public Task<EstadoBackup?> LerAsync() => Task.FromResult(inicial);
        public Task GravarAsync(EstadoBackup estado) => Task.CompletedTask;
    }

    private class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private static BackupBackgroundService Criar(EstadoBackup? estado, DateTimeOffset agora, bool habilitado = true)
    {
        var scopeFactory = new FakeServiceScopeFactory(new FakeServiceProvider(_ => null));

        return new BackupBackgroundService(
            scopeFactory,
            new FakeEstadoStore(estado),
            new BackupOptions { Habilitado = habilitado },
            NullLogger<BackupBackgroundService>.Instance,
            new RelogioFixo(agora));
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_SemExecucaoAnterior_RetornaVerdadeiro()
    {
        var service = Criar(null, DateTimeOffset.UtcNow);

        Assert.True(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_UltimaExecucaoHaMaisDe24h_RetornaVerdadeiro()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-25), 1000);

        var service = Criar(estado, agora);

        Assert.True(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_UltimaExecucaoRecente_RetornaFalso()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-3), 1000);

        var service = Criar(estado, agora);

        Assert.False(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_Desabilitado_RetornaFalso()
    {
        var service = Criar(null, DateTimeOffset.UtcNow, habilitado: false);

        Assert.False(await service.DeveExecutarAgoraAsync());
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~BackupBackgroundServiceTests"`
Expected: FAIL na compilação — `BackupBackgroundService` não existe.

- [ ] **Step 3: Implementar**

Crie `Src/Application/UseCases/Backup/BackupBackgroundService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Dispara o backup uma vez por dia no horário configurado e recupera a
/// execução perdida quando o contêiner reinicia depois da janela.
/// </summary>
public class BackupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEstadoBackupStore estadoStore,
    BackupOptions options,
    ILogger<BackupBackgroundService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly TimeProvider _relogio = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Verdadeiro quando a última execução bem-sucedida tem mais de 24h — ou
    /// nunca houve uma. Evita que um deploy dentro da janela pule a noite.
    /// </summary>
    public async Task<bool> DeveExecutarAgoraAsync()
    {
        if (!options.Habilitado) return false;

        var estado = await estadoStore.LerAsync();
        if (estado is null) return true;

        return _relogio.GetUtcNow().UtcDateTime - estado.UltimaExecucaoUtc > TimeSpan.FromHours(24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Habilitado)
        {
            logger.LogInformation("Backup desabilitado por configuração (BACKUP_ENABLED=false).");
            return;
        }

        if (!options.SegredosPresentes)
        {
            // Agendar sem os segredos só produziria uma falha por noite e
            // afogaria o e-mail de sucesso, que é o nosso sinal de vida.
            logger.LogError(
                "Backup não será agendado: BACKUP_PASSPHRASE, B2_KEY_ID ou B2_APPLICATION_KEY ausente.");
            return;
        }

        logger.LogInformation("BackupBackgroundService iniciado. Horário diário: {Horario}.", options.Horario);

        if (await DeveExecutarAgoraAsync())
        {
            logger.LogInformation("Última execução tem mais de 24h. Executando imediatamente.");
            await ExecutarAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TempoAteProximaExecucao(), _relogio, stoppingToken);
                await ExecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no laço do serviço de backup.");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), _relogio, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("BackupBackgroundService finalizado.");
    }

    private TimeSpan TempoAteProximaExecucao()
    {
        // O contêiner roda em America/Sao_Paulo, então o horário local é o esperado.
        var agora = _relogio.GetLocalNow().DateTime;
        var proxima = agora.Date + options.Horario;

        if (proxima <= agora) proxima = proxima.AddDays(1);

        return proxima - agora;
    }

    private async Task ExecutarAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var executarBackup = scope.ServiceProvider.GetRequiredService<ExecutarBackup>();

        await executarBackup.ExecuteAsync(stoppingToken);
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~BackupBackgroundServiceTests"`
Expected: PASS — 4 testes.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/Backup/BackupBackgroundService.cs Tests/Domain/BackupBackgroundServiceTests.cs
git commit -m "feat: agendamento diario do backup com recuperacao de execucao perdida"
```

---

### Task 6: `SincronizadorUploads` — envio incremental

**Files:**
- Create: `Src/Infrastructure/Backup/SincronizadorUploads.cs`
- Test: `Tests/Domain/SincronizadorUploadsTests.cs`

**Interfaces:**
- Consumes: `ISincronizadorUploads`, `IArmazenamentoBackup` (Task 3), `BackupOptions.CaminhoUploads` (Task 1)
- Produces: `class SincronizadorUploads(IArmazenamentoBackup, BackupOptions, ILogger<SincronizadorUploads>) : ISincronizadorUploads`

- [ ] **Step 1: Escrever os testes que falham**

Crie `Tests/Domain/SincronizadorUploadsTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class SincronizadorUploadsTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"uploads-teste-{Guid.NewGuid()}");

    public SincronizadorUploadsTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private void CriarArquivo(string nome) => File.WriteAllText(Path.Combine(_pasta, nome), "conteudo");

    private class FakeArmazenamento(params string[] chavesExistentes) : IArmazenamentoBackup
    {
        public readonly List<string> ChavesEnviadas = new();

        public Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)
        {
            ChavesEnviadas.Add(chave);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<string>>(chavesExistentes);
    }

    private SincronizadorUploads Criar(IArmazenamentoBackup armazenamento) =>
        new(armazenamento,
            new BackupOptions { CaminhoUploads = _pasta },
            NullLogger<SincronizadorUploads>.Instance);

    [Fact]
    public async Task SincronizarAsync_BucketVazio_EnviaTodosOsArquivos()
    {
        CriarArquivo("a.pdf");
        CriarArquivo("b.pdf");
        var armazenamento = new FakeArmazenamento();

        var enviados = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(2, enviados);
        Assert.Contains("uploads/a.pdf", armazenamento.ChavesEnviadas);
        Assert.Contains("uploads/b.pdf", armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_ArquivoJaNoBucket_EnviaApenasOsAusentes()
    {
        CriarArquivo("a.pdf");
        CriarArquivo("b.pdf");
        var armazenamento = new FakeArmazenamento("uploads/a.pdf");

        var enviados = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(1, enviados);
        Assert.Equal(new[] { "uploads/b.pdf" }, armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_NadaNovo_NaoEnviaNada()
    {
        CriarArquivo("a.pdf");
        var armazenamento = new FakeArmazenamento("uploads/a.pdf");

        var enviados = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(0, enviados);
        Assert.Empty(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_PastaInexistente_RetornaZeroSemErro()
    {
        var armazenamento = new FakeArmazenamento();
        var sincronizador = new SincronizadorUploads(
            armazenamento,
            new BackupOptions { CaminhoUploads = Path.Combine(_pasta, "nao-existe") },
            NullLogger<SincronizadorUploads>.Instance);

        Assert.Equal(0, await sincronizador.SincronizarAsync(CancellationToken.None));
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test --filter "FullyQualifiedName~SincronizadorUploadsTests"`
Expected: FAIL na compilação — `SincronizadorUploads` não existe.

- [ ] **Step 3: Implementar**

Crie `Src/Infrastructure/Backup/SincronizadorUploads.cs`:

```csharp
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Envia para o bucket apenas os arquivos ainda ausentes. Os nomes são GUIDs e
/// nunca são sobrescritos, então comparar chaves basta — não é preciso hash.
/// Nada é apagado no bucket: a retenção é da regra de ciclo de vida.
/// </summary>
public class SincronizadorUploads(
    IArmazenamentoBackup armazenamento,
    BackupOptions options,
    ILogger<SincronizadorUploads> logger) : ISincronizadorUploads
{
    private const string Prefixo = "uploads/";

    public async Task<int> SincronizarAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.CaminhoUploads))
        {
            logger.LogWarning("Pasta de uploads {Caminho} não existe. Sincronização ignorada.", options.CaminhoUploads);
            return 0;
        }

        var remotas = (await armazenamento.ListarChavesAsync(Prefixo, cancellationToken)).ToHashSet();
        var enviados = 0;

        foreach (var caminhoLocal in Directory.EnumerateFiles(options.CaminhoUploads))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chave = Prefixo + Path.GetFileName(caminhoLocal);
            if (remotas.Contains(chave)) continue;

            await armazenamento.EnviarArquivoAsync(chave, caminhoLocal, cancellationToken);
            enviados++;
        }

        return enviados;
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test --filter "FullyQualifiedName~SincronizadorUploadsTests"`
Expected: PASS — 4 testes.

- [ ] **Step 5: Commit**

```bash
git add Src/Infrastructure/Backup/SincronizadorUploads.cs Tests/Domain/SincronizadorUploadsTests.cs
git commit -m "feat: sincronizacao incremental dos uploads para o bucket"
```

---

### Task 7: `ArmazenamentoB2` — cliente S3

O SDK da AWS não é testável de forma útil sem um servidor de verdade, então esta classe fica deliberadamente fina: sem regra de negócio, apenas tradução para o SDK. A validação real acontece na Task 10.

**Files:**
- Modify: `Src/ProximoTurnoApi.csproj` (adicionar `AWSSDK.S3`)
- Create: `Src/Infrastructure/Backup/ArmazenamentoB2.cs`

**Interfaces:**
- Consumes: `IArmazenamentoBackup` (Task 3), `BackupOptions` (Task 1)
- Produces: `class ArmazenamentoB2(BackupOptions, ILogger<ArmazenamentoB2>) : IArmazenamentoBackup, IDisposable`

- [ ] **Step 1: Adicionar o pacote**

```bash
dotnet add Src/ProximoTurnoApi.csproj package AWSSDK.S3
```

Sem fixar versão — o comando resolve a estável mais recente.

- [ ] **Step 2: Implementar**

Crie `Src/Infrastructure/Backup/ArmazenamentoB2.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Armazenamento na Backblaze B2 pela API S3-compatível. A Backblaze exige
/// path-style; virtual-host style não funciona no endpoint deles.
/// </summary>
public class ArmazenamentoB2 : IArmazenamentoBackup, IDisposable
{
    private readonly IAmazonS3 _cliente;
    private readonly BackupOptions _options;
    private readonly ILogger<ArmazenamentoB2> _logger;

    public ArmazenamentoB2(BackupOptions options, ILogger<ArmazenamentoB2> logger)
    {
        _options = options;
        _logger = logger;

        _cliente = new AmazonS3Client(
            options.B2KeyId,
            options.B2ApplicationKey,
            new AmazonS3Config
            {
                ServiceURL = options.B2Endpoint,
                ForcePathStyle = true
            });
    }

    public async Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)
    {
        await _cliente.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.B2Bucket,
            Key = chave,
            FilePath = caminhoLocal
        }, cancellationToken);

        _logger.LogDebug("Objeto {Chave} enviado para o bucket {Bucket}.", chave, _options.B2Bucket);
    }

    public async Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)
    {
        var chaves = new List<string>();
        string? continuacao = null;

        do
        {
            var resposta = await _cliente.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.B2Bucket,
                Prefix = prefixo,
                ContinuationToken = continuacao
            }, cancellationToken);

            chaves.AddRange(resposta.S3Objects.Select(o => o.Key));
            continuacao = resposta.IsTruncated == true ? resposta.NextContinuationToken : null;
        }
        while (continuacao is not null);

        return chaves;
    }

    public void Dispose() => _cliente.Dispose();
}
```

- [ ] **Step 3: Confirmar que compila e que a suíte segue verde**

Run: `dotnet build Src/ProximoTurnoApi.csproj` seguido de `dotnet test`
Expected: build sem erros; todos os testes existentes continuam passando.

- [ ] **Step 4: Commit**

```bash
git add Src/ProximoTurnoApi.csproj Src/Infrastructure/Backup/ArmazenamentoB2.cs
git commit -m "feat: cliente de armazenamento na Backblaze B2 via API S3"
```

---

### Task 8: `DumpBancoMySql` — dump, compressão e cifra

**Files:**
- Modify: `Dockerfile` (adicionar `default-mysql-client`)
- Create: `Src/Infrastructure/Backup/DumpBancoMySql.cs`

**Interfaces:**
- Consumes: `IDumpBanco` + `ResultadoDump` (Task 3), `BackupOptions` (Task 1), `IConfiguration` (para a connection string)
- Produces: `class DumpBancoMySql(IConfiguration, BackupOptions, ILogger<DumpBancoMySql>) : IDumpBanco`

- [ ] **Step 1: Adicionar o cliente MySQL à imagem**

Em `Dockerfile:28-38`, acrescente `default-mysql-client` à lista do `apt-get install` já existente, logo após `ca-certificates`:

```dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    curl \
    wget \
    gnupg \
    ca-certificates \
    default-mysql-client && \
    wget -q https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb && \
    apt-get install -y ./google-chrome-stable_current_amd64.deb && \
    rm google-chrome-stable_current_amd64.deb && \
    ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone && \
    rm -rf /var/lib/apt/lists/*
```

`gnupg` já está na lista, então não é preciso adicionar nada para a cifra.

- [ ] **Step 2: Implementar**

Crie `Src/Infrastructure/Backup/DumpBancoMySql.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Gera o dump com `mysqldump`, comprime com gzip e cifra com GPG simétrico.
/// mysqldump é cliente de rede: conecta no contêiner do MySQL pela connection
/// string, sem precisar de docker exec nem do socket do Docker.
/// </summary>
public class DumpBancoMySql(
    IConfiguration configuration,
    BackupOptions options,
    ILogger<DumpBancoMySql> logger) : IDumpBanco
{
    public async Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken)
    {
        var conexao = LerConexao();
        var destino = Path.Combine(Path.GetTempPath(), $"dump-{Guid.NewGuid()}.sql.gz.gpg");

        // Pipeline em shell: o dump nunca é materializado em claro no disco.
        // --single-transaction dá um snapshot consistente sem travar tabelas.
        var comando =
            $"set -o pipefail; " +
            $"mysqldump --single-transaction --routines --triggers " +
            $"--host='{conexao.Host}' --port={conexao.Porta} " +
            $"--user='{conexao.Usuario}' --password='{conexao.Senha}' '{conexao.Banco}' " +
            $"| gzip " +
            $"| gpg --batch --yes --symmetric --cipher-algo AES256 --passphrase-fd 0 --output '{destino}'";

        var processo = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList = { "-c", comando },
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        processo.Start();

        // A passphrase entra por stdin para não aparecer na linha de comando,
        // que é visível para qualquer processo do contêiner.
        await processo.StandardInput.WriteLineAsync(options.Passphrase);
        processo.StandardInput.Close();

        var erro = await processo.StandardError.ReadToEndAsync(cancellationToken);
        await processo.WaitForExitAsync(cancellationToken);

        if (processo.ExitCode != 0)
        {
            if (File.Exists(destino)) File.Delete(destino);
            throw new InvalidOperationException($"mysqldump falhou (código {processo.ExitCode}): {erro}");
        }

        if (!string.IsNullOrWhiteSpace(erro))
            logger.LogWarning("mysqldump escreveu em stderr: {Erro}", erro);

        var tamanho = new FileInfo(destino).Length;
        if (tamanho == 0)
        {
            File.Delete(destino);
            throw new InvalidOperationException("O dump gerado está vazio.");
        }

        return new ResultadoDump(destino, tamanho);
    }

    private (string Host, int Porta, string Usuario, string Senha, string Banco) LerConexao()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection não configurada.");

        var partes = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

        string Obter(params string[] chaves)
        {
            foreach (var chave in chaves)
                if (partes.TryGetValue(chave, out var valor)) return valor;

            throw new InvalidOperationException($"Connection string sem '{chaves[0]}'.");
        }

        var porta = partes.TryGetValue("port", out var p) && int.TryParse(p, out var n) ? n : 3306;

        return (Obter("server", "host"), porta, Obter("user", "user id", "uid"), Obter("password", "pwd"), Obter("database"));
    }
}
```

- [ ] **Step 3: Confirmar que compila e que a suíte segue verde**

Run: `dotnet build Src/ProximoTurnoApi.csproj` seguido de `dotnet test`
Expected: build sem erros; todos os testes continuam passando.

- [ ] **Step 4: Commit**

```bash
git add Dockerfile Src/Infrastructure/Backup/DumpBancoMySql.cs
git commit -m "feat: dump do MySQL comprimido e cifrado com GPG"
```

---

### Task 9: Registro no DI e no docker-compose

**Files:**
- Modify: `Src/Program.cs:42-52` (região de registros)
- Modify: `docker-compose.yml`

**Interfaces:**
- Consumes: tudo das tasks 1–8
- Produces: nenhuma interface nova

- [ ] **Step 1: Registrar os serviços**

Em `Src/Program.cs`, junto dos demais registros (por volta da linha 52, depois de `AddSingleton<IEmailService, EmailService>()`), acrescente:

```csharp
// Backup automatizado
builder.Services.AddSingleton(sp =>
    BackupOptions.DaConfiguracao(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IEstadoBackupStore>(sp =>
    new EstadoBackupArquivo(sp.GetRequiredService<BackupOptions>().CaminhoEstado));
builder.Services.AddSingleton<IArmazenamentoBackup, ArmazenamentoB2>();
builder.Services.AddScoped<IDumpBanco, DumpBancoMySql>();
builder.Services.AddScoped<ISincronizadorUploads, SincronizadorUploads>();
builder.Services.AddScoped<ExecutarBackup>();
builder.Services.AddHostedService<BackupBackgroundService>();
```

Acrescente os `using` necessários no topo do arquivo:

```csharp
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Backup;
```

`BackupOptions` e `IArmazenamentoBackup` são singletons porque não guardam estado por requisição e o cliente S3 é caro de recriar. `ExecutarBackup` é scoped porque o `BackgroundService` cria um escopo por execução, seguindo o padrão de `ContratoQueueBackgroundService`.

- [ ] **Step 2: Adicionar o volume de estado e as variáveis ao compose**

Em `docker-compose.yml`, no serviço `api`, acrescente as variáveis ao bloco `environment` existente (depois de `AUTENTIQUE_WEBHOOK_SECRET`):

```yaml
      BACKUP_ENABLED: ${BACKUP_ENABLED:-true}
      BACKUP_HORA: ${BACKUP_HORA:-03:00}
      BACKUP_EMAIL_DESTINO: ${BACKUP_EMAIL_DESTINO:-contato@proximoturno.com.br}
      BACKUP_PASSPHRASE: ${BACKUP_PASSPHRASE}
      B2_ENDPOINT: ${B2_ENDPOINT:-https://s3.us-east-005.backblazeb2.com}
      B2_BUCKET: ${B2_BUCKET:-proximo-turno}
      B2_KEY_ID: ${B2_KEY_ID}
      B2_APPLICATION_KEY: ${B2_APPLICATION_KEY}
```

No bloco `volumes` do serviço `api`, acrescente a terceira linha:

```yaml
    volumes:
      - api_uploads:/app/wwwroot/uploads
      - api_keys:/app/keys
      - api_backup_state:/app/backup-state
```

E declare o volume na seção `volumes` do final do arquivo:

```yaml
  api_backup_state:
    driver: local
```

- [ ] **Step 3: Documentar as variáveis no DOCKER.md**

Em `DOCKER.md`, na seção `## Variáveis de ambiente importantes` (linha 65), acrescente ao final da lista:

```markdown
### Backup automatizado

Apenas os três segredos são obrigatórios — o resto tem padrão embutido no código:

- `BACKUP_PASSPHRASE`: senha da cifra GPG do dump. **Sem padrão.**
- `B2_KEY_ID` / `B2_APPLICATION_KEY`: credenciais da Backblaze B2. **Sem padrão.**

Se qualquer um dos três faltar, o serviço registra erro no log e não agenda execuções.

Opcionais, com padrão: `BACKUP_ENABLED` (`true`), `BACKUP_HORA` (`03:00`),
`BACKUP_EMAIL_DESTINO` (`contato@proximoturno.com.br`),
`B2_ENDPOINT` (`https://s3.us-east-005.backblazeb2.com`), `B2_BUCKET` (`proximo-turno`).

**Em desenvolvimento, coloque `BACKUP_ENABLED=false` no `.env` local.** Sem isso o
serviço tenta iniciar, não encontra os segredos e polui o log com erro a cada
inicialização.

A `BACKUP_PASSPHRASE` é o único item cuja perda torna os backups irrecuperáveis:
guarde no gerenciador de senhas **e** numa cópia offline.
```

- [ ] **Step 4: Verificar que o compose é válido**

Run: `docker compose config`
Expected: YAML resolvido sem erro. Avisos sobre `BACKUP_PASSPHRASE`, `B2_KEY_ID` e `B2_APPLICATION_KEY` vazias são esperados se o `.env` local ainda não os tiver.

- [ ] **Step 5: Rodar a suíte completa**

Run: `dotnet test`
Expected: PASS — todos os testes, novos e existentes.

- [ ] **Step 6: Commit**

```bash
git add Src/Program.cs docker-compose.yml DOCKER.md
git commit -m "feat: registra servicos de backup e volume de estado"
```

---

### Task 10: Runbook de restauração e ensaio

**Nenhum backup está pronto antes de uma restauração ter sido testada.** Esta task é obrigatória.

**Files:**
- Create: `docs/RESTORE.md`

- [ ] **Step 1: Escrever o runbook**

Crie `docs/RESTORE.md`:

````markdown
# Restauração do Próximo Turno

Procedimento para reconstruir a API numa máquina nova a partir dos backups na
Backblaze B2. Escrito para ser seguido sem depender de contexto prévio.

## O que você precisa em mãos

- `BACKUP_PASSPHRASE` (gerenciador de senhas ou cópia offline)
- `B2_KEY_ID` e `B2_APPLICATION_KEY`
- O conteúdo do `.env` de produção
- Bucket: `proximo-turno`, endpoint `https://s3.us-east-005.backblazeb2.com`

**A retenção do bucket é de 7 dias.** Só existem os backups da última semana.

## 1. Preparar a máquina

```bash
git clone <url-do-repositorio> && cd ProximoTurnoApi
# Restaure o .env a partir do gerenciador de senhas
```

## 2. Baixar o backup mais recente

```bash
export AWS_ACCESS_KEY_ID=<B2_KEY_ID>
export AWS_SECRET_ACCESS_KEY=<B2_APPLICATION_KEY>
export ENDPOINT=https://s3.us-east-005.backblazeb2.com

# Listar o que existe
aws s3 ls s3://proximo-turno/db/ --endpoint-url $ENDPOINT

# Baixar (troque a data pela mais recente da listagem)
aws s3 cp s3://proximo-turno/db/2026-07-28.sql.gz.gpg . --endpoint-url $ENDPOINT
```

## 3. Decifrar e descomprimir

```bash
gpg --batch --decrypt --passphrase '<BACKUP_PASSPHRASE>' \
    --output backup.sql.gz 2026-07-28.sql.gz.gpg

gunzip backup.sql.gz
```

## 4. Subir o banco e carregar o dump

```bash
docker compose up -d mysql
# Aguarde o healthcheck ficar saudável
docker compose ps

docker compose exec -T mysql mysql -u root -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE" < backup.sql
```

O dump já contém o schema, então **não** rode as migrations antes desta etapa.

## 5. Restaurar os uploads

```bash
docker volume create proximoturnoapi_api_uploads
aws s3 sync s3://proximo-turno/uploads/ ./uploads-restaurados/ --endpoint-url $ENDPOINT

docker run --rm \
  -v proximoturnoapi_api_uploads:/destino \
  -v "$(pwd)/uploads-restaurados":/origem \
  alpine sh -c "cp -a /origem/. /destino/"
```

## 6. Subir a aplicação

```bash
docker compose up -d
curl -f http://localhost/health
```

## 7. Depois de restaurar

- As chaves de Data Protection **não** são backupeadas por decisão de projeto.
  Todos os tokens Bearer emitidos ficam inválidos e os usuários precisam fazer
  login novamente. Isso é esperado.
- Confira se o primeiro backup pós-restauração rodou: o e-mail "Backup OK" deve
  chegar na manhã seguinte.
````

- [ ] **Step 2: Executar o ensaio de restauração**

Faça a restauração de verdade, contra um MySQL descartável, e corrija o runbook onde ele estiver errado ou incompleto:

```bash
# Sobe um MySQL isolado, sem tocar em nada de produção
docker run -d --name mysql-ensaio \
  -e MYSQL_ROOT_PASSWORD=ensaio \
  -e MYSQL_DATABASE=proximoturno \
  -p 127.0.0.1:3399:3306 \
  mysql:9.0

# Siga os passos 2 e 3 do runbook para obter backup.sql, depois carregue:
docker exec -i mysql-ensaio mysql -u root -pensaio proximoturno < backup.sql

# Confira que os dados chegaram
docker exec -i mysql-ensaio mysql -u root -pensaio proximoturno \
  -e "SELECT COUNT(*) FROM Clientes; SELECT COUNT(*) FROM Pedidos;"

# Limpar
docker rm -f mysql-ensaio
```

Expected: as contagens batem com produção.

- [ ] **Step 3: Commit**

```bash
git add docs/RESTORE.md
git commit -m "docs: runbook de restauracao validado por ensaio"
```

---

## Verificação final

Depois da Task 10, confirme na primeira noite em produção:

- [ ] O e-mail "Backup OK" chegou em `contato@proximoturno.com.br`.
- [ ] O objeto `db/AAAA-MM-DD.sql.gz.gpg` existe no bucket com tamanho plausível.
- [ ] O prefixo `uploads/` tem a mesma quantidade de arquivos que o volume.
- [ ] Na segunda noite, o e-mail chegou de novo e nenhum upload duplicado foi enviado (o contador de uploads novos deve ser 0 se ninguém subiu manual novo).
