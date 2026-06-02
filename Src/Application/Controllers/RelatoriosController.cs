using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Identity;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/relatorios")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class RelatoriosController(ILogger<ControllerBasico> logger, ObterRelatorioFaturamento obterRelatorioFaturamento) : ControllerBasico(logger)
{
    private readonly ObterRelatorioFaturamento _obterRelatorioFaturamento = obterRelatorioFaturamento;

    [HttpGet("faturamento")]
    public async Task<IActionResult> ObterFaturamento([FromQuery] DateOnly? data_inicial, [FromQuery] DateOnly? data_final)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var relatorio = await _obterRelatorioFaturamento.ExecuteAsync(data_inicial, data_final);
            return Ok(ApiResultDTO<DashboardReportDTO>.CreateSuccessResult(relatorio, "Relatório de faturamento obtido com sucesso"));
        });
    }
}
