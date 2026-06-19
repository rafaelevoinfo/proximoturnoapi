using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            var contrato = await _gerarContratoUseCase.ExecuteAsync(User, idPedido);

            if (!_gerarContratoUseCase.IsValid) {
                var notification = _gerarContratoUseCase.Notifications.FirstOrDefault();
                return notification?.Type switch {
                    UseCaseNotificationType.Forbid =>
                        Forbid(),
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
            var contrato = await _consultarContratoUseCase.ExecuteAsync(User, idPedido);

            if (!_consultarContratoUseCase.IsValid) {
                var notification = _consultarContratoUseCase.Notifications.FirstOrDefault();
                if (notification?.Type == UseCaseNotificationType.Forbid) {
                    return Forbid();
                }
                return NotFound(ApiResultDTO<ContratoDTO>.CreateFailureResult(_consultarContratoUseCase.AggregateErrors()));
            }

            var dto = ContratoDTO.FromModel(contrato!);
            return Ok(ApiResultDTO<ContratoDTO>.CreateSuccessResult(dto, "Contrato encontrado"));
        });
    }
}
