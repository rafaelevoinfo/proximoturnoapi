using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Controllers;

[ApiController]
[Authorize]
public class ComentariosController(
    ILogger<ControllerBasico> logger,
    SalvarComentario salvarComentarioUseCase,
    ObterComentariosPedido obterComentariosPedidoUseCase,
    ObterComentarioPorId obterComentarioPorIdUseCase,
    ExcluirComentario excluirComentarioUseCase) : ControllerBasico(logger)
{
    [HttpPost("api/pedido/{pedidoId:int}/comentario")]
    public async Task<IActionResult> SalvarComentario([FromRoute] int pedidoId, [FromBody] SalvarComentarioDTO dto)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var res = await salvarComentarioUseCase.ExecuteAsync(User, pedidoId, dto);
            if (!salvarComentarioUseCase.IsValid)
            {
                var errorNotification = salvarComentarioUseCase.Notifications.First();
                return errorNotification.Type switch
                {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<ComentarioDTO>.CreateFailureResult(salvarComentarioUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<ComentarioDTO>.CreateFailureResult(salvarComentarioUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<ComentarioDTO>.CreateSuccessResult(res, "Comentário salvo com sucesso"));
        });
    }

    [HttpGet("api/pedido/{pedidoId:int}")]
    public async Task<IActionResult> ObterComentariosPedido([FromRoute] int pedidoId)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var res = await obterComentariosPedidoUseCase.ExecuteAsync(User, pedidoId);
            if (!obterComentariosPedidoUseCase.IsValid)
            {
                var errorNotification = obterComentariosPedidoUseCase.Notifications.First();
                return errorNotification.Type switch
                {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<List<ComentarioDTO>>.CreateFailureResult(obterComentariosPedidoUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<List<ComentarioDTO>>.CreateFailureResult(obterComentariosPedidoUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<List<ComentarioDTO>>.CreateSuccessResult(res, "Comentários do pedido obtidos com sucesso"));
        });
    }

    [HttpGet("api/comentario/{id:int}")]
    public async Task<IActionResult> ObterComentarioPorId([FromRoute] int id)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var res = await obterComentarioPorIdUseCase.ExecuteAsync(User, id);
            if (!obterComentarioPorIdUseCase.IsValid)
            {
                var errorNotification = obterComentarioPorIdUseCase.Notifications.First();
                return errorNotification.Type switch
                {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<ComentarioDTO>.CreateFailureResult(obterComentarioPorIdUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<ComentarioDTO>.CreateFailureResult(obterComentarioPorIdUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<ComentarioDTO>.CreateSuccessResult(res, "Comentário obtido com sucesso"));
        });
    }

    [HttpDelete("api/comentario/{id:int}")]
    public async Task<IActionResult> ExcluirComentario([FromRoute] int id)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var res = await excluirComentarioUseCase.ExecuteAsync(User, id);
            if (!excluirComentarioUseCase.IsValid)
            {
                var errorNotification = excluirComentarioUseCase.Notifications.First();
                return errorNotification.Type switch
                {
                    UseCaseNotificationType.NotFound => NotFound(ApiResultDTO<bool>.CreateFailureResult(excluirComentarioUseCase.AggregateErrors())),
                    UseCaseNotificationType.Forbid => Forbid(),
                    _ => BadRequest(ApiResultDTO<bool>.CreateFailureResult(excluirComentarioUseCase.AggregateErrors()))
                };
            }
            return Ok(ApiResultDTO<bool>.CreateSuccessResult(res, "Comentário excluído com sucesso"));
        });
    }
}
