using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ProcessarWebhookAutentique : UseCaseBasico {
    private readonly IContratoRepository contratoRepository;
    private readonly IConfiguration? configuration;
    private readonly ILogger<ProcessarWebhookAutentique> logger;

    public ProcessarWebhookAutentique(
        IContratoRepository contratoRepository,
        ILogger<ProcessarWebhookAutentique> logger)
        : this(contratoRepository, null, logger) { }

    public ProcessarWebhookAutentique(
        IContratoRepository contratoRepository,
        IConfiguration? configuration,
        ILogger<ProcessarWebhookAutentique> logger) {
        this.contratoRepository = contratoRepository;
        this.configuration = configuration;
        this.logger = logger;
    }

    /// <summary>
    /// Processa um evento de webhook recebido do Autentique.
    /// A estrutura do payload segue a documentação oficial:
    /// https://docs.autentique.com.br/api/integration-basics/webhooks
    ///
    /// Payload raiz contém: id, object, name, format, url, event { id, object, organization, type, data, previous_attributes, created_at }
    ///
    /// Eventos tratados:
    /// - signature.accepted: signatário assinou o documento
    /// - signature.rejected: signatário recusou o documento
    /// - document.finished: todos os signatários concluíram (todas assinaturas finalizadas)
    /// </summary>
    public async Task ExecuteAsync(string rawBody, string? signatureHeader = null) {
        logger.LogInformation("Webhook Autentique recebido: {Body}", rawBody);

        var webhookSecret = configuration?["AUTENTIQUE_WEBHOOK_SECRET"];
        logger.LogInformation("Configuração - AUTENTIQUE_WEBHOOK_SECRET: '{Secret}'", webhookSecret);
        logger.LogInformation("Header recebido - x-autentique-signature: '{Signature}'", signatureHeader);

        if (!string.IsNullOrEmpty(webhookSecret)) {
            if (string.IsNullOrEmpty(signatureHeader)) {
                logger.LogWarning("Webhook Autentique recebido sem header x-autentique-signature");
                throw new UnauthorizedAccessException("Webhook recebido sem cabeçalho de assinatura.");
            }

            var calculatedSignature = CalcularHmacSha256(rawBody, webhookSecret);
            logger.LogInformation("Assinatura calculada: '{Calculated}'", calculatedSignature);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(calculatedSignature),
                Encoding.UTF8.GetBytes(signatureHeader))) {
                logger.LogWarning("Webhook Autentique recebido com assinatura HMAC inválida");
                throw new UnauthorizedAccessException("Assinatura HMAC inválida.");
            }
        }

        try {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Extrair o evento conforme estrutura documentada: root.event
            if (!root.TryGetProperty("event", out var eventEl)) {
                logger.LogWarning("Webhook recebido sem objeto 'event'");
                return;
            }

            // Extrair tipo do evento: event.type
            string? eventType = null;
            if (eventEl.TryGetProperty("type", out var typeEl)) {
                eventType = typeEl.GetString();
            }

            if (string.IsNullOrEmpty(eventType)) {
                logger.LogWarning("Webhook recebido sem event.type");
                return;
            }

            // Extrair o document ID dependendo do tipo de evento
            string? documentId = ExtrairDocumentId(eventEl, eventType);

            if (string.IsNullOrEmpty(documentId)) {
                logger.LogWarning("Webhook recebido sem document ID identificável. EventType={EventType}", eventType);
                return;
            }

            logger.LogInformation("Processando webhook: DocumentId={DocId}, EventType={EventType}", documentId, eventType);

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

            // Determina ação baseada no tipo do evento
            switch (eventType) {
                case "signature.accepted":
                case "document.finished":
                    contrato.Status = StatusContrato.Assinado;
                    contrato.DataAssinatura = DateTime.Now;
                    await contratoRepository.SaveAsync(contrato);
                    logger.LogInformation("Contrato do pedido {IdPedido} marcado como ASSINADO via webhook (evento: {EventType})",
                        contrato.IdPedido, eventType);
                    break;

                case "signature.rejected":
                    contrato.Status = StatusContrato.Rejeitado;
                    await contratoRepository.SaveAsync(contrato);
                    logger.LogInformation("Contrato do pedido {IdPedido} marcado como REJEITADO via webhook (evento: {EventType})",
                        contrato.IdPedido, eventType);
                    break;

                default:
                    logger.LogInformation("Evento de webhook não alterou status do contrato: {EventType}", eventType);
                    break;
            }

        } catch (JsonException ex) {
            logger.LogError(ex, "Erro ao processar o payload do webhook");
        }
    }

    /// <summary>
    /// Extrai o document ID do payload do evento, de acordo com o tipo de evento.
    ///
    /// Para eventos de signature (signature.*): event.data.document contém o hash ID do documento.
    /// Para eventos de document (document.*): event.data.id contém o hash ID do documento.
    /// </summary>
    private static string? ExtrairDocumentId(JsonElement eventEl, string eventType) {
        if (!eventEl.TryGetProperty("data", out var dataEl)) {
            return null;
        }

        if (eventType.StartsWith("signature.", StringComparison.OrdinalIgnoreCase)) {
            // Evento de assinatura: event.data.document = "hash_do_documento"
            if (dataEl.TryGetProperty("document", out var docEl) && docEl.ValueKind == JsonValueKind.String) {
                return docEl.GetString();
            }
        }

        if (eventType.StartsWith("document.", StringComparison.OrdinalIgnoreCase)) {
            // Evento de documento: event.data.id = "hash_do_documento"
            if (dataEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String) {
                return idEl.GetString();
            }
        }

        // Fallback: tenta ambos os caminhos
        if (dataEl.TryGetProperty("document", out var fallbackDocEl) && fallbackDocEl.ValueKind == JsonValueKind.String) {
            return fallbackDocEl.GetString();
        }
        if (dataEl.TryGetProperty("id", out var fallbackIdEl) && fallbackIdEl.ValueKind == JsonValueKind.String) {
            return fallbackIdEl.GetString();
        }

        return null;
    }

    private static string CalcularHmacSha256(string payload, string secret) {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexStringLower(hashBytes);
    }
}
