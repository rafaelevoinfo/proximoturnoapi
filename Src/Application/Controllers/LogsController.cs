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

    /// <param name="niveis">Níveis separados por vírgula (ex.: Warning,Error).</param>
    /// <param name="inicio">Início do período em ISO 8601 com fuso (ex.: 2026-10-03T14:00:00-03:00).</param>
    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] string? arquivo, [FromQuery] DateTimeOffset? inicio,
        [FromQuery] DateTimeOffset? fim, [FromQuery] string? niveis, [FromQuery] string? busca,
        [FromQuery] string? trace_id, [FromQuery] int? limite, CancellationToken ct)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var filtro = new FiltroLogs {
                Arquivo = arquivo,
                Inicio = inicio,
                Fim = fim,
                Niveis = niveis?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Busca = busca,
                TraceId = trace_id,
                Limite = limite,
            };
            var consulta = await _consultarLogs.ExecuteAsync(filtro, ct);
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
