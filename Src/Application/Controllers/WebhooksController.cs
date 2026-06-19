using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Controllers;

/// <summary>
/// Controller para receber webhooks de serviços externos.
/// NÃO usa [Authorize] — webhooks são chamados por servidores externos.
/// Autenticação via HMAC no header x-autentique-signature (conforme documentação Autentique).
/// </summary>
[Route("api/webhooks")]
[ApiController]
public class WebhooksController(
    ILogger<ControllerBasico> logger,
    ProcessarWebhookAutentique _webhookUseCase,
    IConfiguration configuration) : ControllerBasico(logger) {

    [HttpPost("autentique")]
    public async Task<IActionResult> ReceberWebhookAutentique() {
        return await EncapsulateRequestAsync(async () => {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body)) {
                return BadRequest("Body vazio");
            }

            // Validação HMAC via header x-autentique-signature (conforme documentação oficial)
            var webhookSecret = configuration["AUTENTIQUE_WEBHOOK_SECRET"];
            if (!string.IsNullOrEmpty(webhookSecret)) {
                var signature = Request.Headers["x-autentique-signature"].ToString();

                if (string.IsNullOrEmpty(signature)) {
                    _logger.LogWarning("Webhook Autentique recebido sem header x-autentique-signature");
                    return Unauthorized();
                }

                var calculatedSignature = CalcularHmacSha256(body, webhookSecret);

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(calculatedSignature),
                    Encoding.UTF8.GetBytes(signature))) {
                    _logger.LogWarning("Webhook Autentique recebido com assinatura HMAC inválida");
                    return Unauthorized();
                }
            }

            await _webhookUseCase.ExecuteAsync(body);

            // Sempre retorna 200 para o Autentique não reenviar
            return Ok();
        });
    }

    /// <summary>
    /// Calcula o HMAC-SHA256 do payload usando o webhook secret.
    /// Conforme documentação do Autentique: hash_hmac('sha256', payload, secret)
    /// </summary>
    private static string CalcularHmacSha256(string payload, string secret) {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexStringLower(hashBytes);
    }
}
