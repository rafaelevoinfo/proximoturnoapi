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
public class RelatoriosController(ILogger<RelatoriosController> logger, ObterRelatorioFaturamento obterRelatorioFaturamento, ObterRelatorioCustosIa obterRelatorioCustosIa) : ControllerBasico(logger)
{
    private readonly ObterRelatorioFaturamento _obterRelatorioFaturamento = obterRelatorioFaturamento;
    private readonly ObterRelatorioCustosIa _obterRelatorioCustosIa = obterRelatorioCustosIa;

    [HttpGet("faturamento")]
    public async Task<IActionResult> ObterFaturamento([FromQuery] DateOnly? data_inicial, [FromQuery] DateOnly? data_final)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var relatorio = await _obterRelatorioFaturamento.ExecuteAsync(data_inicial, data_final);
            return Ok(ApiResultDTO<DashboardReportDTO>.CreateSuccessResult(relatorio, "Relatório de faturamento obtido com sucesso"));
        });
    }

    [HttpGet("custos-ia")]
    public async Task<IActionResult> ObterCustosIa([FromQuery] DateOnly? data_inicial, [FromQuery] DateOnly? data_final, [FromQuery] string? modelo)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var relatorio = await _obterRelatorioCustosIa.ExecuteAsync(data_inicial, data_final, modelo);
            return Ok(ApiResultDTO<RelatorioCustosIaDTO>.CreateSuccessResult(relatorio, "Relatório de custos de IA obtido com sucesso"));
        });
    }
}
