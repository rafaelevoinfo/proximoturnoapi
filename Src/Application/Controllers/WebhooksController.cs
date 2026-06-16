using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
                _logger.LogWarning("Webhook Autentique recebido with secret inválido");
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
