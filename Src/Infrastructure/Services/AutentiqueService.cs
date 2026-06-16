using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
                    sandbox: true,
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
            document = new { name = nomeDocumento },
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

        var signature = doc.Signatures.LastOrDefault()
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
