using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/lista-desejos")]
[ApiController]
[Authorize]
public class ListaDesejosController(GerenciarListaDesejos _useCase, ILogger<ListaDesejosController> _logger) : ControllerBasico(_logger) {

    [HttpGet]
    public async Task<IActionResult> Get() {
        return await EncapsulateRequestAsync(async () => {
            var items = await _useCase.GetWishlistAsync(User);
            if (!_useCase.IsValid) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_useCase.AggregateErrors()));
            }
            var result = items.Select(i => JogoPublicDTO.FromModel(i.Jogo!)).ToList();
            return Ok(ApiResultDTO<List<JogoPublicDTO>>.CreateSuccessResult(result, "Lista de desejos recuperada com sucesso."));
        });
    }

    [HttpGet("{idJogo:int}/status")]
    public async Task<IActionResult> GetStatus([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var inWishlist = await _useCase.IsInWishlistAsync(User, idJogo);
            if (!_useCase.IsValid) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_useCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<bool>.CreateSuccessResult(inWishlist, "Status verificado."));
        });
    }

    [HttpPost("{idJogo:int}")]
    public async Task<IActionResult> Post([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var result = await _useCase.AddToWishlistAsync(User, idJogo);
            if (!result) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_useCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo adicionado à lista de desejos."));
        });
    }

    [HttpDelete("{idJogo:int}")]
    public async Task<IActionResult> Delete([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var result = await _useCase.RemoveFromWishlistAsync(User, idJogo);
            if (!result) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_useCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo removido da lista de desejos."));
        });
    }
}
