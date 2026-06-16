# Integração Autentique — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrar o backend com a API do Autentique para gerar contratos PDF de aluguel e enviá-los para assinatura digital.

**Architecture:** Service Layer (AutentiqueService + ContratoPdfService) chamados por Use Cases dedicados, expostos via Controllers REST. Modelo `ContratoAutentique` no banco rastreia documentos enviados. Webhook recebe notificações de assinatura.

**Tech Stack:** .NET 10, PuppeteerSharp (HTML→PDF), HttpClient (GraphQL multipart), Entity Framework Core (MySQL)

---

## Task 1: NuGet Package + Configuração

**Files:**
- Modify: `Src/ProximoTurnoApi.csproj`
- Modify: `Src/.env.example`

- [ ] **Step 1: Adicionar pacote PuppeteerSharp**

```bash
cd ProximoTurnoApi
dotnet add Src/ProximoTurnoApi.csproj package PuppeteerSharp
```

- [ ] **Step 2: Adicionar variáveis ao .env.example**

Adicionar ao final do arquivo `Src/.env.example`:

```
# Autentique Configuration
AUTENTIQUE_API_TOKEN=seu_token_aqui
AUTENTIQUE_SANDBOX=true
AUTENTIQUE_WEBHOOK_SECRET=gerar_um_secret_aleatorio
```

- [ ] **Step 3: Adicionar as mesmas variáveis ao .env local**

Adicionar ao final do arquivo `Src/.env`:

```
# Autentique Configuration
AUTENTIQUE_API_TOKEN=
AUTENTIQUE_SANDBOX=true
AUTENTIQUE_WEBHOOK_SECRET=local_dev_secret
```

- [ ] **Step 4: Commit**

```bash
git add Src/ProximoTurnoApi.csproj Src/.env.example
git commit -m "chore: add PuppeteerSharp package and Autentique config vars"
```

---

## Task 2: Modelo ContratoAutentique + Migration

**Files:**
- Create: `Src/Infrastructure/Models/ContratoAutentique.cs`
- Modify: `Src/Infrastructure/Repositories/DatabaseContext.cs`

- [ ] **Step 1: Criar o enum StatusContrato e o model ContratoAutentique**

Criar `Src/Infrastructure/Models/ContratoAutentique.cs`:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum StatusContrato : short {
    Pendente = 0,
    Assinado = 1,
    Rejeitado = 2
}

[Table("CONTRATO_AUTENTIQUE")]
public class ContratoAutentique : BaseModel {
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    public Domain.Pedido Pedido { get; set; } = null!;

    [Column("AUTENTIQUE_DOCUMENT_ID"), MaxLength(100)]
    public required string AutentiqueDocumentId { get; set; }

    [Column("AUTENTIQUE_PUBLIC_ID"), MaxLength(100)]
    public required string AutentiquePublicId { get; set; }

    [Column("LINK_ASSINATURA"), MaxLength(500)]
    public required string LinkAssinatura { get; set; }

    [Column("STATUS")]
    public StatusContrato Status { get; set; } = StatusContrato.Pendente;

    [Column("DATA_CRIACAO")]
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    [Column("DATA_ASSINATURA")]
    public DateTime? DataAssinatura { get; set; }
}
```

- [ ] **Step 2: Adicionar DbSet e configuração no DatabaseContext**

No arquivo `Src/Infrastructure/Repositories/DatabaseContext.cs`:

1. Adicionar o `DbSet` junto com os outros (após a linha do `DbSet<Cupom>`):

```csharp
public DbSet<ContratoAutentique> ContratosAutentique { get; set; }
```

2. Adicionar chamada de configuração no `OnModelCreating`, após `ConfigureCupom(modelBuilder);`:

```csharp
ConfigureContratoAutentique(modelBuilder);
```

3. Adicionar o método de configuração (antes do bloco dos DbSets):

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

- [ ] **Step 3: Gerar a migration**

```bash
cd ProximoTurnoApi
dotnet ef migrations add AddContratoAutentique --project Src/ProximoTurnoApi.csproj
```

Expected: Migration criada em `Src/Migrations/` sem erros.

- [ ] **Step 4: Commit**

```bash
git add Src/Infrastructure/Models/ContratoAutentique.cs Src/Infrastructure/Repositories/DatabaseContext.cs Src/Migrations/
git commit -m "feat: add ContratoAutentique model and migration"
```

---

## Task 3: ContratoRepository

**Files:**
- Create: `Src/Infrastructure/Repositories/ContratoRepository.cs`

- [ ] **Step 1: Criar interface e implementação do ContratoRepository**

Criar `Src/Infrastructure/Repositories/ContratoRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IContratoRepository : IBaseRepository {
    Task SaveAsync(ContratoAutentique contrato, bool commit = true);
    Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido);
    Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId);
}

public class ContratoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IContratoRepository {

    public async Task SaveAsync(ContratoAutentique contrato, bool commit) {
        await SaveChangesAsync(_dbContext.ContratosAutentique, contrato, commit);
    }

    public async Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) {
        return await _dbContext.ContratosAutentique
            .Include(c => c.Pedido)
            .AsTracking()
            .FirstOrDefaultAsync(c => c.IdPedido == idPedido);
    }

    public async Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId) {
        return await _dbContext.ContratosAutentique
            .AsTracking()
            .FirstOrDefaultAsync(c => c.AutentiqueDocumentId == autentiqueDocumentId);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Infrastructure/Repositories/ContratoRepository.cs
git commit -m "feat: add ContratoRepository"
```

---

## Task 4: Template HTML do Contrato

**Files:**
- Create: `Src/Templates/contrato-aluguel.html`
- Modify: `Src/ProximoTurnoApi.csproj`

- [ ] **Step 1: Criar o template HTML com placeholders**

Criar `Src/Templates/contrato-aluguel.html`:

```html
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 12px;
            line-height: 1.6;
            color: #333;
            padding: 40px 50px;
        }
        .header {
            text-align: center;
            margin-bottom: 30px;
            border-bottom: 2px solid #2c3e50;
            padding-bottom: 15px;
        }
        .header h1 { font-size: 20px; color: #2c3e50; margin-bottom: 5px; }
        .header p { font-size: 11px; color: #7f8c8d; }
        h2 {
            font-size: 14px; color: #2c3e50;
            margin: 20px 0 10px 0;
            border-bottom: 1px solid #bdc3c7;
            padding-bottom: 5px;
        }
        .info-grid { display: flex; flex-wrap: wrap; gap: 8px 30px; margin-bottom: 10px; }
        .info-item { min-width: 200px; }
        .info-item strong { color: #2c3e50; }
        table { width: 100%; border-collapse: collapse; margin: 10px 0; }
        th, td { border: 1px solid #bdc3c7; padding: 8px 10px; text-align: left; }
        th { background-color: #2c3e50; color: white; font-weight: 600; }
        tr:nth-child(even) { background-color: #f8f9fa; }
        .totals { text-align: right; margin: 15px 0; }
        .totals p { margin: 3px 0; }
        .totals .total-final { font-size: 14px; font-weight: bold; color: #2c3e50; }
        .terms { margin: 20px 0; font-size: 11px; text-align: justify; }
        .terms ol { padding-left: 20px; }
        .terms li { margin-bottom: 6px; }
        .signature-section { margin-top: 40px; text-align: center; }
        .signature-line {
            margin-top: 50px; border-top: 1px solid #333;
            width: 300px; display: inline-block; padding-top: 5px;
        }
        .footer { margin-top: 30px; text-align: center; font-size: 10px; color: #95a5a6; }
    </style>
</head>
<body>
    <div class="header">
        <h1>PRÓXIMO TURNO</h1>
        <p>Contrato de Locação de Jogos de Tabuleiro</p>
    </div>

    <p><strong>Contrato Nº:</strong> {{NUMERO_PEDIDO}} &nbsp;&nbsp; <strong>Data:</strong> {{DATA_PEDIDO}}</p>

    <h2>1. DADOS DO LOCATÁRIO</h2>
    <div class="info-grid">
        <div class="info-item"><strong>Nome:</strong> {{NOME_CLIENTE}}</div>
        <div class="info-item"><strong>Telefone:</strong> {{TELEFONE_CLIENTE}}</div>
        <div class="info-item"><strong>E-mail:</strong> {{EMAIL_CLIENTE}}</div>
        <div class="info-item"><strong>Endereço:</strong> {{ENDERECO_CLIENTE}}</div>
    </div>

    <h2>2. ITENS LOCADOS</h2>
    <table>
        <thead>
            <tr>
                <th>#</th>
                <th>Jogo</th>
                <th>Valor</th>
                <th>Data Devolução</th>
            </tr>
        </thead>
        <tbody>
            {{TABELA_ITENS}}
        </tbody>
    </table>

    <div class="totals">
        <p><strong>Método de Pagamento:</strong> {{METODO_PAGAMENTO}}</p>
        <p><strong>Método de Entrega:</strong> {{METODO_ENTREGA}}</p>
        <p><strong>Desconto:</strong> R$ {{VALOR_DESCONTO}}</p>
        <p class="total-final">VALOR TOTAL: R$ {{VALOR_TOTAL}}</p>
    </div>

    <h2>3. TERMOS E CONDIÇÕES</h2>
    <div class="terms">
        <ol>
            <li>O LOCATÁRIO se compromete a devolver os jogos na data estipulada, em perfeito estado de conservação.</li>
            <li>Em caso de atraso na devolução, será cobrada multa diária conforme tabela vigente.</li>
            <li>Danos, perdas ou extravio de peças serão de responsabilidade do LOCATÁRIO, que deverá arcar com os custos de reposição.</li>
            <li>O LOCATÁRIO declara ter recebido os jogos em bom estado e com todas as peças conferidas.</li>
            <li>A LOCADORA reserva-se o direito de recusar futuras locações em caso de descumprimento deste contrato.</li>
            <li>O presente contrato é válido a partir da data de sua assinatura até a devolução de todos os itens locados.</li>
        </ol>
    </div>

    <div class="signature-section">
        <p>{{CIDADE}}, {{DATA_ATUAL}}</p>
        <div class="signature-line">
            {{NOME_CLIENTE}}<br>
            <small>Locatário</small>
        </div>
    </div>

    <div class="footer">
        <p>Próximo Turno — Locação de Jogos de Tabuleiro</p>
        <p>Documento gerado automaticamente em {{DATA_ATUAL}}</p>
    </div>
</body>
</html>
```

- [ ] **Step 2: Garantir que o template seja copiado para o output (csproj)**

Adicionar ao `Src/ProximoTurnoApi.csproj`, dentro de um `<ItemGroup>`:

```xml
<ItemGroup>
    <Content Include="Templates\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 3: Commit**

```bash
git add Src/Templates/contrato-aluguel.html Src/ProximoTurnoApi.csproj
git commit -m "feat: add HTML contract template"
```

---

## Task 5: ContratoPdfService

**Files:**
- Create: `Src/Infrastructure/Services/ContratoPdfService.cs`

- [ ] **Step 1: Criar o ContratoPdfService**

Criar `Src/Infrastructure/Services/ContratoPdfService.cs`:

```csharp
using System.Globalization;
using System.Text;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public interface IContratoPdfService {
    Task<byte[]> GerarPdfAsync(Domain.Pedido pedido);
}

public class ContratoPdfService(ILogger<ContratoPdfService> logger, IWebHostEnvironment environment) : IContratoPdfService {

    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<byte[]> GerarPdfAsync(Domain.Pedido pedido) {
        var templatePath = Path.Combine(environment.ContentRootPath, "Templates", "contrato-aluguel.html");
        var templateHtml = await File.ReadAllTextAsync(templatePath);

        var html = SubstituirPlaceholders(templateHtml, pedido);

        return await ConverterHtmlParaPdfAsync(html);
    }

    private static string SubstituirPlaceholders(string html, Domain.Pedido pedido) {
        var cliente = pedido.Cliente;
        var agora = DateTime.Now;

        var tabelaItens = new StringBuilder();
        var index = 1;
        foreach (var item in pedido.Items) {
            var nomeJogo = item.JogoCopia?.Jogo?.Nome ?? "N/A";
            tabelaItens.AppendLine($"""
                <tr>
                    <td>{index}</td>
                    <td>{nomeJogo}</td>
                    <td>R$ {item.Valor.ToString("N2", PtBr)}</td>
                    <td>{item.DataDevolucao.ToString("dd/MM/yyyy", PtBr)}</td>
                </tr>
            """);
            index++;
        }

        return html
            .Replace("{{NUMERO_PEDIDO}}", pedido.Id.ToString())
            .Replace("{{DATA_PEDIDO}}", pedido.DataHora.ToString("dd/MM/yyyy", PtBr))
            .Replace("{{NOME_CLIENTE}}", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cliente.Nome))
            .Replace("{{TELEFONE_CLIENTE}}", cliente.Telefone)
            .Replace("{{EMAIL_CLIENTE}}", cliente.Email)
            .Replace("{{ENDERECO_CLIENTE}}", cliente.Endereco)
            .Replace("{{TABELA_ITENS}}", tabelaItens.ToString())
            .Replace("{{VALOR_TOTAL}}", pedido.ValorTotal.ToString("N2", PtBr))
            .Replace("{{VALOR_DESCONTO}}", pedido.ValorDesconto.ToString("N2", PtBr))
            .Replace("{{METODO_PAGAMENTO}}", pedido.MetodoPagamento ?? "Não informado")
            .Replace("{{METODO_ENTREGA}}", pedido.MetodoEntrega ?? "Não informado")
            .Replace("{{CIDADE}}", "Sua Cidade") // TODO: configurar via appsettings
            .Replace("{{DATA_ATUAL}}", agora.ToString("dd 'de' MMMM 'de' yyyy", PtBr));
    }

    private async Task<byte[]> ConverterHtmlParaPdfAsync(string html) {
        logger.LogInformation("Iniciando conversão HTML para PDF via PuppeteerSharp");

        using var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions {
            WaitUntil = [WaitUntilNavigation.NetworkIdle]
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            }
        });

        logger.LogInformation("PDF gerado com sucesso ({Bytes} bytes)", pdfBytes.Length);
        return pdfBytes;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Infrastructure/Services/ContratoPdfService.cs
git commit -m "feat: add ContratoPdfService for HTML-to-PDF generation"
```

---

## Task 6: AutentiqueService

**Files:**
- Create: `Src/Infrastructure/Services/AutentiqueService.cs`

- [ ] **Step 1: Criar DTOs de resposta internos e o AutentiqueService**

Criar `Src/Infrastructure/Services/AutentiqueService.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProximoTurnoApi.Infrastructure.Services;

// --- DTOs internos para deserializar respostas do Autentique ---

public record AutentiqueDocumentResult(string DocumentId, string PublicId, string SigningLink);

public record AutentiqueDocumentStatus(
    string DocumentId,
    string? SignedAt,
    string? RejectedAt,
    string? ViewedAt,
    string? SigningLink
);

// Classes para deserialização JSON do Autentique
file record AutentiqueGraphQlResponse<T>(T Data, List<AutentiqueError>? Errors);
file record AutentiqueError(string Message);

file record CreateDocumentData(
    [property: JsonPropertyName("createDocument")] CreateDocumentPayload CreateDocument
);
file record CreateDocumentPayload(
    string Id,
    string Name,
    List<SignaturePayload> Signatures
);
file record SignaturePayload(
    [property: JsonPropertyName("public_id")] string PublicId,
    string? Name,
    string? Email,
    SignatureLinkPayload? Link
);
file record SignatureLinkPayload(
    [property: JsonPropertyName("short_link")] string ShortLink
);

file record DocumentQueryData(DocumentPayload Document);
file record DocumentPayload(
    string Id,
    List<SignatureDetailPayload> Signatures
);
file record SignatureDetailPayload(
    [property: JsonPropertyName("public_id")] string PublicId,
    SignatureLinkPayload? Link,
    TimestampPayload? Viewed,
    TimestampPayload? Signed,
    TimestampPayload? Rejected
);
file record TimestampPayload(
    [property: JsonPropertyName("created_at")] string CreatedAt
);

file record CreateLinkData(
    [property: JsonPropertyName("createLinkToSignature")] SignatureLinkPayload CreateLinkToSignature
);

// --- Service ---

public interface IAutentiqueService {
    Task<AutentiqueDocumentResult> CriarDocumentoAsync(byte[] pdfBytes, string nomeDocumento, string nomeSignatario, bool sandbox);
    Task<AutentiqueDocumentStatus> ConsultarDocumentoAsync(string documentId);
    Task<string> ObterLinkAssinaturaAsync(string publicId);
}

public class AutentiqueService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AutentiqueService> logger) : IAutentiqueService {

    private const string GraphQlEndpoint = "https://api.autentique.com.br/v2/graphql";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private string GetApiToken() {
        var token = configuration["AUTENTIQUE_API_TOKEN"];
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("AUTENTIQUE_API_TOKEN não configurado.");
        return token;
    }

    /// <summary>
    /// Cria um documento no Autentique com upload do PDF via GraphQL multipart request spec.
    /// O signatário recebe um link direto (DELIVERY_METHOD_LINK) — sem envio de email.
    /// </summary>
    public async Task<AutentiqueDocumentResult> CriarDocumentoAsync(byte[] pdfBytes, string nomeDocumento, string nomeSignatario, bool sandbox) {
        logger.LogInformation("Criando documento no Autentique: {Nome}, Sandbox: {Sandbox}", nomeDocumento, sandbox);

        const string mutation = """
            mutation CreateDocumentMutation(
                $document: DocumentInput!,
                $signers: [SignerInput!]!,
                $file: Upload!
            ) {
                createDocument(
                    document: $document,
                    signers: $signers,
                    file: $file
                ) {
                    id
                    name
                    signatures {
                        public_id
                        name
                        email
                        link {
                            short_link
                        }
                    }
                }
            }
            """;

        var variables = new {
            document = new { name = nomeDocumento, sandbox },
            signers = new[] {
                new {
                    name = nomeSignatario,
                    action = "SIGN",
                    delivery_method = "DELIVERY_METHOD_LINK"
                }
            },
            file = (object?)null
        };

        var operations = JsonSerializer.Serialize(new { query = mutation, variables });

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(operations, Encoding.UTF8, "application/json"), "operations");
        content.Add(new StringContent("{\"0\": [\"variables.file\"]}", Encoding.UTF8, "application/json"), "map");

        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "0", $"{nomeDocumento}.pdf");

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetApiToken());

        var response = await client.PostAsync(GraphQlEndpoint, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) {
            logger.LogError("Erro ao criar documento no Autentique: {Status} - {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Erro na API do Autentique: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AutentiqueGraphQlResponse<CreateDocumentData>>(responseBody, JsonOptions);

        if (result?.Errors is { Count: > 0 }) {
            var errorMsg = string.Join(", ", result.Errors.Select(e => e.Message));
            logger.LogError("Erro GraphQL do Autentique: {Errors}", errorMsg);
            throw new InvalidOperationException($"Erro GraphQL do Autentique: {errorMsg}");
        }

        var doc = result?.Data?.CreateDocument
            ?? throw new InvalidOperationException("Resposta inesperada do Autentique: createDocument é nulo.");

        var signature = doc.Signatures.FirstOrDefault()
            ?? throw new InvalidOperationException("Nenhuma assinatura retornada pelo Autentique.");

        var signingLink = signature.Link?.ShortLink
            ?? throw new InvalidOperationException("Link de assinatura não retornado pelo Autentique.");

        logger.LogInformation("Documento criado no Autentique: {DocId}, PublicId: {PublicId}", doc.Id, signature.PublicId);

        return new AutentiqueDocumentResult(doc.Id, signature.PublicId, signingLink);
    }

    /// <summary>
    /// Consulta o status de um documento no Autentique.
    /// </summary>
    public async Task<AutentiqueDocumentStatus> ConsultarDocumentoAsync(string documentId) {
        logger.LogInformation("Consultando documento no Autentique: {DocId}", documentId);

        const string query = """
            query {
                document(id: "%DOCUMENT_ID%") {
                    id
                    signatures {
                        public_id
                        link { short_link }
                        viewed { created_at }
                        signed { created_at }
                        rejected { created_at }
                    }
                }
            }
            """;

        var body = JsonSerializer.Serialize(new {
            query = query.Replace("%DOCUMENT_ID%", documentId)
        });

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetApiToken());

        var response = await client.PostAsync(GraphQlEndpoint, new StringContent(body, Encoding.UTF8, "application/json"));
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) {
            logger.LogError("Erro ao consultar documento: {Status} - {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Erro na API do Autentique: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AutentiqueGraphQlResponse<DocumentQueryData>>(responseBody, JsonOptions);

        if (result?.Errors is { Count: > 0 }) {
            var errorMsg = string.Join(", ", result.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Erro GraphQL: {errorMsg}");
        }

        var doc = result?.Data?.Document
            ?? throw new InvalidOperationException("Documento não encontrado no Autentique.");

        var sig = doc.Signatures.FirstOrDefault();

        return new AutentiqueDocumentStatus(
            DocumentId: doc.Id,
            SignedAt: sig?.Signed?.CreatedAt,
            RejectedAt: sig?.Rejected?.CreatedAt,
            ViewedAt: sig?.Viewed?.CreatedAt,
            SigningLink: sig?.Link?.ShortLink
        );
    }

    /// <summary>
    /// Regenera/obtém o link de assinatura para uma assinatura existente.
    /// </summary>
    public async Task<string> ObterLinkAssinaturaAsync(string publicId) {
        logger.LogInformation("Obtendo link de assinatura para PublicId: {PublicId}", publicId);

        const string mutation = """
            mutation {
                createLinkToSignature(public_id: "%PUBLIC_ID%") {
                    short_link
                }
            }
            """;

        var body = JsonSerializer.Serialize(new {
            query = mutation.Replace("%PUBLIC_ID%", publicId)
        });

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetApiToken());

        var response = await client.PostAsync(GraphQlEndpoint, new StringContent(body, Encoding.UTF8, "application/json"));
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"Erro na API do Autentique: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AutentiqueGraphQlResponse<CreateLinkData>>(responseBody, JsonOptions);

        return result?.Data?.CreateLinkToSignature?.ShortLink
            ?? throw new InvalidOperationException("Link de assinatura não retornado pelo Autentique.");
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Infrastructure/Services/AutentiqueService.cs
git commit -m "feat: add AutentiqueService GraphQL client"
```

---

## Task 7: DTOs

**Files:**
- Create: `Src/Application/DTOs/ContratoDTO.cs`

- [ ] **Step 1: Criar DTOs de contrato**

Criar `Src/Application/DTOs/ContratoDTO.cs`:

```csharp
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record ContratoDTO {
    public int Id { get; init; }
    public int IdPedido { get; init; }
    public string LinkAssinatura { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime DataCriacao { get; init; }
    public DateTime? DataAssinatura { get; init; }

    public static ContratoDTO FromModel(ContratoAutentique contrato) {
        return new ContratoDTO {
            Id = contrato.Id,
            IdPedido = contrato.IdPedido,
            LinkAssinatura = contrato.LinkAssinatura,
            Status = contrato.Status.ToString(),
            DataCriacao = contrato.DataCriacao,
            DataAssinatura = contrato.DataAssinatura
        };
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/DTOs/ContratoDTO.cs
git commit -m "feat: add ContratoDTO"
```

---

## Task 8: GerarContratoPedido UseCase

**Files:**
- Create: `Src/Application/UseCases/Contrato/GerarContratoPedido.cs`

- [ ] **Step 1: Criar o use case GerarContratoPedido**

Criar `Src/Application/UseCases/Contrato/GerarContratoPedido.cs`:

```csharp
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class GerarContratoPedido(
    IPedidoRepository pedidoRepository,
    IContratoRepository contratoRepository,
    IContratoPdfService contratoPdfService,
    IAutentiqueService autentiqueService,
    IConfiguration configuration,
    ILogger<GerarContratoPedido> logger) : UseCaseBasico {

    public async Task<ContratoAutentique?> ExecuteAsync(int idPedido) {
        // 1. Verificar se já existe contrato para este pedido
        var contratoExistente = await contratoRepository.GetByPedidoIdAsync(idPedido);
        if (contratoExistente is not null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Já existe um contrato gerado para este pedido."));
            return contratoExistente;
        }

        // 2. Buscar o pedido completo
        var pedido = await pedidoRepository.GetByIdAsync(idPedido);
        if (pedido is null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound,
                "Pedido não encontrado."));
            return null;
        }

        if (pedido.Items.Count == 0) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Não é possível gerar contrato para um pedido sem itens."));
            return null;
        }

        // 3. Gerar o PDF do contrato
        logger.LogInformation("Gerando PDF do contrato para o pedido {IdPedido}", idPedido);
        byte[] pdfBytes;
        try {
            pdfBytes = await contratoPdfService.GerarPdfAsync(pedido);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro ao gerar PDF do contrato para o pedido {IdPedido}", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.Error,
                "Erro ao gerar o PDF do contrato."));
            return null;
        }

        // 4. Enviar para o Autentique
        var sandbox = string.Equals(configuration["AUTENTIQUE_SANDBOX"], "true", StringComparison.OrdinalIgnoreCase);
        var nomeDocumento = $"Contrato Aluguel - Pedido #{idPedido}";
        var nomeSignatario = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pedido.Cliente.Nome);

        AutentiqueDocumentResult resultado;
        try {
            resultado = await autentiqueService.CriarDocumentoAsync(pdfBytes, nomeDocumento, nomeSignatario, sandbox);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro ao enviar contrato ao Autentique para o pedido {IdPedido}", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.Error,
                "Erro ao enviar o contrato para assinatura digital."));
            return null;
        }

        // 5. Salvar no banco
        var contrato = new ContratoAutentique {
            IdPedido = idPedido,
            AutentiqueDocumentId = resultado.DocumentId,
            AutentiquePublicId = resultado.PublicId,
            LinkAssinatura = resultado.SigningLink,
            Status = StatusContrato.Pendente,
            DataCriacao = DateTime.Now
        };

        await contratoRepository.SaveAsync(contrato);

        logger.LogInformation("Contrato criado com sucesso para o pedido {IdPedido}: DocId={DocId}", idPedido, resultado.DocumentId);
        return contrato;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/UseCases/Contrato/GerarContratoPedido.cs
git commit -m "feat: add GerarContratoPedido use case"
```

---

## Task 9: ConsultarContratoPedido UseCase

**Files:**
- Create: `Src/Application/UseCases/Contrato/ConsultarContratoPedido.cs`

- [ ] **Step 1: Criar o use case ConsultarContratoPedido**

Criar `Src/Application/UseCases/Contrato/ConsultarContratoPedido.cs`:

```csharp
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class ConsultarContratoPedido(
    IContratoRepository contratoRepository,
    IAutentiqueService autentiqueService,
    ILogger<ConsultarContratoPedido> logger) : UseCaseBasico {

    /// <summary>
    /// Consulta o contrato de um pedido. Se o contrato estiver pendente, consulta o status
    /// atualizado no Autentique e atualiza o registro local se necessário.
    /// </summary>
    public async Task<ContratoAutentique?> ExecuteAsync(int idPedido) {
        var contrato = await contratoRepository.GetByPedidoIdAsync(idPedido);
        if (contrato is null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound,
                "Nenhum contrato encontrado para este pedido."));
            return null;
        }

        // Se já está em estado final, retorna direto
        if (contrato.Status != StatusContrato.Pendente) {
            return contrato;
        }

        // Consulta status atualizado no Autentique
        try {
            var status = await autentiqueService.ConsultarDocumentoAsync(contrato.AutentiqueDocumentId);

            if (status.SignedAt is not null) {
                contrato.Status = StatusContrato.Assinado;
                contrato.DataAssinatura = DateTime.Now;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como assinado via consulta", idPedido);
            } else if (status.RejectedAt is not null) {
                contrato.Status = StatusContrato.Rejeitado;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como rejeitado via consulta", idPedido);
            }

            // Atualiza o link se mudou
            if (status.SigningLink is not null && status.SigningLink != contrato.LinkAssinatura) {
                contrato.LinkAssinatura = status.SigningLink;
                await contratoRepository.SaveAsync(contrato);
            }
        } catch (Exception ex) {
            // Log mas não falha - retorna o que temos no banco
            logger.LogWarning(ex, "Não foi possível consultar status do contrato no Autentique para o pedido {IdPedido}", idPedido);
        }

        return contrato;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/UseCases/Contrato/ConsultarContratoPedido.cs
git commit -m "feat: add ConsultarContratoPedido use case"
```

---

## Task 10: ProcessarWebhookAutentique UseCase

**Files:**
- Create: `Src/Application/UseCases/Contrato/ProcessarWebhookAutentique.cs`

- [ ] **Step 1: Criar o use case ProcessarWebhookAutentique**

Criar `Src/Application/UseCases/Contrato/ProcessarWebhookAutentique.cs`:

```csharp
using System.Text.Json;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ProcessarWebhookAutentique(
    IContratoRepository contratoRepository,
    ILogger<ProcessarWebhookAutentique> logger) : UseCaseBasico {

    /// <summary>
    /// Processa um evento de webhook recebido do Autentique.
    /// O payload exato pode variar; fazemos parsing defensivo e logamos o body cru.
    /// </summary>
    public async Task ExecuteAsync(string rawBody) {
        logger.LogInformation("Webhook Autentique recebido: {Body}", rawBody);

        try {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Tenta extrair o document ID do payload
            // A estrutura exata do webhook deve ser validada durante testes com o Autentique
            string? documentId = null;
            string? eventType = null;

            if (root.TryGetProperty("document", out var documentEl)) {
                if (documentEl.TryGetProperty("id", out var idEl)) {
                    documentId = idEl.GetString();
                }
            }

            if (root.TryGetProperty("event", out var eventEl)) {
                eventType = eventEl.GetString();
            }

            // Fallback: tenta pegar de outros formatos possíveis
            if (documentId is null && root.TryGetProperty("document_id", out var docIdEl)) {
                documentId = docIdEl.GetString();
            }

            if (documentId is null) {
                logger.LogWarning("Webhook recebido sem document ID identificável");
                return;
            }

            logger.LogInformation("Processando webhook: DocumentId={DocId}, Event={Event}", documentId, eventType);

            var contrato = await contratoRepository.GetByAutentiqueDocumentIdAsync(documentId);
            if (contrato is null) {
                logger.LogWarning("Contrato não encontrado para DocumentId: {DocId}", documentId);
                return;
            }

            // Já em estado final, ignora
            if (contrato.Status != StatusContrato.Pendente) {
                logger.LogInformation("Contrato já em estado final ({Status}), ignorando webhook", contrato.Status);
                return;
            }

            // Determina ação baseada no evento
            var eventLower = eventType?.ToLowerInvariant() ?? "";
            if (eventLower.Contains("accepted") || eventLower.Contains("finished") || eventLower.Contains("signed")) {
                contrato.Status = StatusContrato.Assinado;
                contrato.DataAssinatura = DateTime.Now;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como ASSINADO via webhook", contrato.IdPedido);
            } else if (eventLower.Contains("rejected")) {
                contrato.Status = StatusContrato.Rejeitado;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como REJEITADO via webhook", contrato.IdPedido);
            } else {
                logger.LogInformation("Evento de webhook não alterou status: {Event}", eventType);
            }

        } catch (JsonException ex) {
            logger.LogError(ex, "Erro ao parsear payload do webhook");
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/UseCases/Contrato/ProcessarWebhookAutentique.cs
git commit -m "feat: add ProcessarWebhookAutentique use case"
```

---

## Task 11: ContratosController

**Files:**
- Create: `Src/Application/Controllers/ContratosController.cs`

- [ ] **Step 1: Criar o ContratosController**

Criar `Src/Application/Controllers/ContratosController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/contratos")]
[ApiController]
[Authorize]
public class ContratosController(
    ILogger<ControllerBasico> logger,
    GerarContratoPedido _gerarContratoUseCase,
    ConsultarContratoPedido _consultarContratoUseCase) : ControllerBasico(logger) {

    /// <summary>
    /// Gera o contrato de aluguel para um pedido e envia para assinatura digital no Autentique.
    /// Retorna o link de assinatura para ser aberto no frontend.
    /// </summary>
    [HttpPost("pedido/{idPedido:int}")]
    public async Task<IActionResult> GerarContrato([FromRoute] int idPedido) {
        return await EncapsulateRequestAsync(async () => {
            var contrato = await _gerarContratoUseCase.ExecuteAsync(idPedido);

            if (!_gerarContratoUseCase.IsValid) {
                var notification = _gerarContratoUseCase.Notifications.FirstOrDefault();
                return notification?.Type switch {
                    UseCaseNotificationType.NotFound =>
                        NotFound(ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors())),
                    UseCaseNotificationType.Error =>
                        StatusCode(500, ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors())),
                    _ =>
                        BadRequest(ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors()))
                };
            }

            if (contrato is not null) {
                var dto = ContratoDTO.FromModel(contrato);
                return Ok(ApiResultDTO<ContratoDTO>.CreateSuccessResult(dto, "Contrato gerado com sucesso"));
            }

            return StatusCode(500, ApiResultDTO<ContratoDTO>.CreateFailureResult("Erro inesperado ao gerar contrato"));
        });
    }

    /// <summary>
    /// Consulta o contrato de um pedido, incluindo status atualizado e link de assinatura.
    /// </summary>
    [HttpGet("pedido/{idPedido:int}")]
    public async Task<IActionResult> ConsultarContrato([FromRoute] int idPedido) {
        return await EncapsulateRequestAsync(async () => {
            var contrato = await _consultarContratoUseCase.ExecuteAsync(idPedido);

            if (!_consultarContratoUseCase.IsValid) {
                return NotFound(ApiResultDTO<ContratoDTO>.CreateFailureResult(_consultarContratoUseCase.AggregateErrors()));
            }

            var dto = ContratoDTO.FromModel(contrato!);
            return Ok(ApiResultDTO<ContratoDTO>.CreateSuccessResult(dto, "Contrato encontrado"));
        });
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/Controllers/ContratosController.cs
git commit -m "feat: add ContratosController"
```

---

## Task 12: WebhooksController

**Files:**
- Create: `Src/Application/Controllers/WebhooksController.cs`

- [ ] **Step 1: Criar o WebhooksController**

Criar `Src/Application/Controllers/WebhooksController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Controllers;

/// <summary>
/// Controller para receber webhooks de serviços externos.
/// NÃO usa [Authorize] — webhooks são chamados por servidores externos.
/// Autenticação via webhook secret na query string.
/// </summary>
[Route("api/webhooks")]
[ApiController]
public class WebhooksController(
    ILogger<ControllerBasico> logger,
    ProcessarWebhookAutentique _webhookUseCase,
    IConfiguration configuration) : ControllerBasico(logger) {

    [HttpPost("autentique")]
    public async Task<IActionResult> ReceberWebhookAutentique([FromQuery] string? secret) {
        return await EncapsulateRequestAsync(async () => {
            // Validação do secret
            var expectedSecret = configuration["AUTENTIQUE_WEBHOOK_SECRET"];
            if (!string.IsNullOrEmpty(expectedSecret) && secret != expectedSecret) {
                _logger.LogWarning("Webhook Autentique recebido com secret inválido");
                return Unauthorized();
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body)) {
                return BadRequest("Body vazio");
            }

            await _webhookUseCase.ExecuteAsync(body);

            // Sempre retorna 200 para o Autentique não reenviar
            return Ok();
        });
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Src/Application/Controllers/WebhooksController.cs
git commit -m "feat: add WebhooksController for Autentique callbacks"
```

---

## Task 13: Registro DI no Program.cs

**Files:**
- Modify: `Src/Program.cs`

- [ ] **Step 1: Registrar os novos serviços, repositórios e use cases**

No `Src/Program.cs`, localizar a seção onde os repositórios são registrados (procure `AddScoped<IPedidoRepository`) e adicionar:

```csharp
// Contrato Repository
builder.Services.AddScoped<IContratoRepository, ContratoRepository>();
```

Localizar a seção onde os serviços são registrados (procure `AddSingleton<CloudinaryService>`) e adicionar:

```csharp
// Autentique + PDF Services
builder.Services.AddHttpClient(); // necessário para IHttpClientFactory
builder.Services.AddScoped<IAutentiqueService, AutentiqueService>();
builder.Services.AddScoped<IContratoPdfService, ContratoPdfService>();
```

Localizar a seção onde os use cases são registrados (procure `AddScoped<CadastroPedido>`) e adicionar:

```csharp
// Contrato Use Cases
builder.Services.AddScoped<GerarContratoPedido>();
builder.Services.AddScoped<ConsultarContratoPedido>();
builder.Services.AddScoped<ProcessarWebhookAutentique>();
```

Garantir que os `using` necessários estão presentes no topo do arquivo:

```csharp
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Infrastructure.Repositories;
```

- [ ] **Step 2: Verificar que o projeto compila**

```bash
cd ProximoTurnoApi
dotnet build Src/ProximoTurnoApi.csproj
```

Expected: Build succeeded. 0 Warning(s). 0 Error(s).

- [ ] **Step 3: Commit**

```bash
git add Src/Program.cs
git commit -m "feat: register Autentique services and use cases in DI"
```

---

## Task 14: Teste Manual End-to-End

- [ ] **Step 1: Garantir que o .env local tem o token do Autentique configurado**

Editar `Src/.env` e preencher `AUTENTIQUE_API_TOKEN` com um token válido da conta Autentique.

- [ ] **Step 2: Iniciar o backend**

```bash
cd ProximoTurnoApi
docker-compose up -d  # banco de dados
dotnet run --project Src/ProximoTurnoApi.csproj
```

- [ ] **Step 3: Aplicar a migration**

A migration deve ser aplicada automaticamente. Se não, executar:

```bash
dotnet ef database update --project Src/ProximoTurnoApi.csproj
```

- [ ] **Step 4: Testar via Scalar/Swagger**

1. Acessar `http://localhost:5016/scalar/v1`
2. Autenticar com um token de admin
3. Criar um pedido de teste (se necessário)
4. Chamar `POST /api/contratos/pedido/{id}` com o ID do pedido
5. Verificar que retorna `{ success: true, data: { linkAssinatura: "https://..." } }`
6. Abrir o `linkAssinatura` no navegador para verificar que a página de assinatura do Autentique carrega
7. Chamar `GET /api/contratos/pedido/{id}` para verificar que retorna o contrato com status "Pendente"

- [ ] **Step 5: Verificar logs**

Verificar nos logs do Serilog que as mensagens de criação do documento aparecem corretamente.
