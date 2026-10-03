using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/logs")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class LogsController(ILogger<LogsController> logger, ConsultarLogs consultarLogs) : ControllerBasico(logger)
{
    private readonly ConsultarLogs _consultarLogs = consultarLogs;

    [HttpGet("arquivos")]
    public async Task<IActionResult> ListarArquivos()
    {
        return await EncapsulateRequestAsync(() =>
        {
            var arquivos = _consultarLogs.ListarArquivos();
            return Task.FromResult<IActionResult>(Ok(ApiResultDTO<IReadOnlyList<ArquivoLogDTO>>.CreateSuccessResult(arquivos)));
        });
    }

    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] string? arquivo, [FromQuery] string? nivel_minimo,
        [FromQuery] string? busca, [FromQuery] string? trace_id, [FromQuery] int? limite, CancellationToken ct)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var consulta = await _consultarLogs.ExecuteAsync(arquivo, nivel_minimo, busca, trace_id, limite, ct);
            return Ok(ApiResultDTO<ConsultaLogsDTO>.CreateSuccessResult(consulta));
        });
    }

    [HttpGet("arquivos/{nome}")]
    public async Task<IActionResult> Baixar(string nome)
    {
        return await EncapsulateRequestAsync(() =>
            Task.FromResult<IActionResult>(File(_consultarLogs.Abrir(nome), "text/plain; charset=utf-8", nome)));
    }
}
