using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/comentarios")]
[ApiController]
[Authorize]
public class ComentariosController(
    ILogger<ControllerBasico> logger,
    SalvarComentario salvarComentarioUseCase,
    PodeComentarJogo podeComentarJogoUseCase,
    ObterComentariosJogo obterComentariosJogoUseCase,
    ObterComentariosFiltrados obterComentariosFiltradosUseCase,
    AtualizarStatusComentario atualizarStatusComentarioUseCase,
    ObterComentarioPorId obterComentarioPorIdUseCase,
    ExcluirComentario excluirComentarioUseCase) : ControllerBasico(logger) {

    [HttpPost]
    public async Task<IActionResult> SalvarComentario([FromBody] SalvarComentarioDTO dto) {
        return await EncapsulateRequestAsync(async () => {
            var id = await salvarComentarioUseCase.ExecuteAsync(User, dto);
            if (!salvarComentarioUseCase.IsValid) {
                var errorNotification = salvarComentarioUseCase.Notifications.First();
                return errorNotification.Type switch {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<int?>.CreateFailureResult(salvarComentarioUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<int?>.CreateFailureResult(salvarComentarioUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<int?>.CreateSuccessResult(id, "Comentário enviado para análise"));
        });
    }

    [HttpGet("jogo/{jogoId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> ObterComentariosJogo([FromRoute] int jogoId, [FromQuery] int? qtde = 3) {
        return await EncapsulateRequestAsync(async () => {
            var res = await obterComentariosJogoUseCase.ExecuteAsync(jogoId, qtde);
            return Ok(ApiResultDTO<List<ComentarioDTO>>.CreateSuccessResult(res, "Comentários do jogo obtidos com sucesso"));
        });
    }

    [HttpGet("jogo/{jogoId:int}/pode-comentar")]
    public async Task<IActionResult> PodeComentarJogo([FromRoute] int jogoId) {
        return await EncapsulateRequestAsync(async () => {
            var res = await podeComentarJogoUseCase.ExecuteAsync(User, jogoId);
            return Ok(ApiResultDTO<bool>.CreateSuccessResult(res, "Verificação de elegibilidade concluída"));
        });
    }

    [HttpGet("pendentes")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ObterComentariosFiltrados([FromQuery] ComentarioFiltersDTO filters) {
        return await EncapsulateRequestAsync(async () => {
            var res = await obterComentariosFiltradosUseCase.ExecuteAsync(filters);
            return Ok(ApiResultDTO<List<ComentarioDTO>>.CreateSuccessResult(res, "Comentários obtidos com sucesso"));
        });
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AtualizarStatusComentario([FromRoute] int id, [FromBody] AtualizarStatusComentarioDTO dto) {
        return await EncapsulateRequestAsync(async () => {
            var res = await atualizarStatusComentarioUseCase.ExecuteAsync(id, dto.Status);
            if (!atualizarStatusComentarioUseCase.IsValid) {
                var errorNotification = atualizarStatusComentarioUseCase.Notifications.First();
                return errorNotification.Type switch {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<ComentarioDTO>.CreateFailureResult(atualizarStatusComentarioUseCase.AggregateErrors())),
                    _ => BadRequest(ApiResultDTO<ComentarioDTO>.CreateFailureResult(atualizarStatusComentarioUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<ComentarioDTO>.CreateSuccessResult(res, "Status do comentário atualizado com sucesso"));
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObterComentarioPorId([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var res = await obterComentarioPorIdUseCase.ExecuteAsync(User, id);
            if (!obterComentarioPorIdUseCase.IsValid) {
                var errorNotification = obterComentarioPorIdUseCase.Notifications.First();
                return errorNotification.Type switch {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<ComentarioDTO>.CreateFailureResult(obterComentarioPorIdUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<ComentarioDTO>.CreateFailureResult(obterComentarioPorIdUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<ComentarioDTO>.CreateSuccessResult(res, "Comentário obtido com sucesso"));
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> ExcluirComentario([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var res = await excluirComentarioUseCase.ExecuteAsync(User, id);
            if (!excluirComentarioUseCase.IsValid) {
                var errorNotification = excluirComentarioUseCase.Notifications.First();
                return errorNotification.Type switch {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<bool>.CreateFailureResult(excluirComentarioUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<bool>.CreateFailureResult(excluirComentarioUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<bool>.CreateSuccessResult(res, "Comentário excluído com sucesso"));
        });
    }
}
