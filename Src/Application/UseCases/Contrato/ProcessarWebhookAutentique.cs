using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
