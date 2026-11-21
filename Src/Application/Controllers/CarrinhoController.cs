using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.Controllers;

public record ReservaCarrinhoDTO(int IdJogo, DateTimeOffset ExpiraEm);

/// <summary>
/// Reservas do carrinho: ao entrar no carrinho o jogo fica segurado por 15 minutos para quem
/// o colocou. Sem login: o carrinho é identificado pelo header X-Carrinho-Id.
/// </summary>
[Route("api/carrinho/reservas")]
[ApiController]
public class CarrinhoController(ILogger<CarrinhoController> logger,
                                ReservarJogoCarrinho _reservarUseCase,
                                IReservasCarrinho _reservas) : ControllerBasico(logger) {

    private const string SemCarrinho = $"Informe o id do carrinho no header {CarrinhoHeader.Nome}.";

    [HttpGet]
    public IActionResult Listar() {
        if (CarrinhoHeader.Ler(Request) is not { } idCarrinho) {
            return BadRequest(ApiResultDTO<object>.CreateFailureResult(SemCarrinho));
        }
        var reservas = _reservas.DoCarrinho(idCarrinho).Select(r => new ReservaCarrinhoDTO(r.Key, r.Value)).ToList();
        return Ok(ApiResultDTO<List<ReservaCarrinhoDTO>>.CreateSuccessResult(reservas));
    }

    [HttpPost("{idJogo:int}")]
    public async Task<IActionResult> Reservar([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            if (CarrinhoHeader.Ler(Request) is not { } idCarrinho) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(SemCarrinho));
            }
            var fim = await _reservarUseCase.ExecuteAsync(idCarrinho, idJogo);
            if (fim is null) {
                return Conflict(ApiResultDTO<ReservaCarrinhoDTO>.CreateFailureResult(_reservarUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ReservaCarrinhoDTO>.CreateSuccessResult(new ReservaCarrinhoDTO(idJogo, fim.Value), "Jogo reservado no carrinho."));
        });
    }

    [HttpDelete("{idJogo:int}")]
    public IActionResult Liberar([FromRoute] int idJogo) {
        if (CarrinhoHeader.Ler(Request) is { } idCarrinho) {
            _reservas.Liberar(idCarrinho, idJogo);
        }
        return NoContent();
    }

    [HttpDelete]
    public IActionResult LiberarTudo() {
        if (CarrinhoHeader.Ler(Request) is { } idCarrinho) {
            _reservas.LiberarTudo(idCarrinho);
        }
        return NoContent();
    }
}
