using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/loja")]
[ApiController]
public class LojaController(ILogger<LojaController> logger,
                            ObterStatusLoja _obterStatusLoja,
                            AtualizarStatusLoja _atualizarStatusLoja) : ControllerBasico(logger) {

    /// <summary>Público: o catálogo consulta antes de o visitante entrar.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus() =>
        await EncapsulateRequestAsync(async () => {
            var status = await _obterStatusLoja.ExecuteAsync();
            return Ok(ApiResultDTO<StatusLojaDTO>.CreateSuccessResult(status, "Status da loja recuperado com sucesso."));
        });

    [HttpPut("status")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PutStatus([FromBody] AtualizarStatusLojaDTO dto) =>
        await EncapsulateRequestAsync(async () => {
            var status = await _atualizarStatusLoja.ExecuteAsync(dto);
            if (!_atualizarStatusLoja.IsValid) {
                return BadRequest(ApiResultDTO<StatusLojaDTO>.CreateFailureResult(_atualizarStatusLoja.AggregateErrors()));
            }
            return Ok(ApiResultDTO<StatusLojaDTO>.CreateSuccessResult(status!, status!.LojaFechada ? "Loja fechada." : "Loja aberta."));
        });
}
