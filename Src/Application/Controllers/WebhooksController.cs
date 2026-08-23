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
    ILogger<WebhooksController> logger,
    ProcessarWebhookAutentique _webhookUseCase) : ControllerBasico(logger) {

    [HttpPost("autentique")]
    public async Task<IActionResult> ReceberWebhookAutentique() {
        return await EncapsulateRequestAsync(async () => {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body)) {
                return BadRequest("Body vazio");
            }

            var signature = Request.Headers["x-autentique-signature"].ToString();

            try {
                await _webhookUseCase.ExecuteAsync(body, signature);
            } catch (UnauthorizedAccessException ex) {
                _logger.LogWarning(ex, "Assinatura do webhook inválida ou não autorizada");
                return Unauthorized();
            }

            // Sempre retorna 200 para o Autentique não reenviar
            return Ok();
        });
    }
}
