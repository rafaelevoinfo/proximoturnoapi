using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.Controllers;

/// <summary>Inscrição dos aparelhos do admin nas notificações push de novos pedidos.</summary>
[Route("api/push")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class PushController(ILogger<PushController> logger,
                            ConfiguracaoPush _configuracao,
                            UserManager<Usuario> _userManager,
                            IInscricaoPushRepository _repository,
                            RegistrarInscricaoPush _registrarInscricao) : ControllerBasico(logger) {

    [HttpGet("chave-publica")]
    public IActionResult GetChavePublica() {
        if (!_configuracao.Configurado) {
            return BadRequest(ApiResultDTO<string>.CreateFailureResult("Notificações push não configuradas no servidor."));
        }
        return Ok(ApiResultDTO<string>.CreateSuccessResult(_configuracao.ChavePublica!, "Chave pública recuperada com sucesso."));
    }

    [HttpPost("inscricoes")]
    public async Task<IActionResult> PostInscricao([FromBody] InscricaoPushDTO dto) {
        return await EncapsulateRequestAsync(async () => {
            var idUsuario = _userManager.GetUserId(User);
            if (idUsuario is null) {
                return Unauthorized();
            }
            if (!await _registrarInscricao.ExecuteAsync(idUsuario, dto, Request.Headers.UserAgent.ToString())) {
                return BadRequest(ApiResultDTO<string>.CreateFailureResult(_registrarInscricao.AggregateErrors()));
            }
            return Ok(ApiResultDTO<string>.CreateSuccessResult(null, "Notificações ativadas neste aparelho."));
        });
    }

    [HttpDelete("inscricoes")]
    public async Task<IActionResult> DeleteInscricao([FromBody] RemoverInscricaoPushDTO dto) {
        return await EncapsulateRequestAsync(async () => {
            var idUsuario = _userManager.GetUserId(User);
            if (idUsuario is null) {
                return Unauthorized();
            }
            // Desativar o que já não existe dá no mesmo: o aparelho fica sem notificações.
            await _repository.RemoverAsync(dto.Endpoint, idUsuario);
            return Ok(ApiResultDTO<string>.CreateSuccessResult(null, "Notificações desativadas neste aparelho."));
        });
    }
}
