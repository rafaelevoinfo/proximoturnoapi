using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

/// <summary>Consulta, registro a registro, das chamadas a LLM e das conversas do chat de regras.</summary>
[Route("api/ia")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class AuditoriaIaController(ILogger<AuditoriaIaController> logger, ListarUsoLlm listarUsoLlm,
    AuditoriaConversasChat auditoriaConversas) : ControllerBasico(logger)
{
    private readonly ListarUsoLlm _listarUsoLlm = listarUsoLlm;
    private readonly AuditoriaConversasChat _auditoriaConversas = auditoriaConversas;

    [HttpGet("requisicoes")]
    public async Task<IActionResult> ListarRequisicoes([FromQuery] DateOnly? data_inicial, [FromQuery] DateOnly? data_final,
        [FromQuery] OperacaoLlm? operacao, [FromQuery] DesfechoLlm? desfecho, [FromQuery] string? modelo,
        [FromQuery] string? busca, [FromQuery] string? trace_id, [FromQuery] OrdemUsoLlm? ordem,
        [FromQuery] int? pagina, [FromQuery] int? tamanho, CancellationToken ct)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var consulta = await _listarUsoLlm.ExecuteAsync(new FiltroUsoLlm {
                DataInicial = data_inicial,
                DataFinal = data_final,
                Operacao = operacao,
                Desfecho = desfecho,
                Modelo = modelo,
                Busca = busca,
                TraceId = trace_id,
                Ordem = ordem ?? OrdemUsoLlm.Recentes,
                Pagina = pagina ?? 1,
                Tamanho = tamanho ?? 50,
            }, ct);
            return Ok(ApiResultDTO<ConsultaUsoLlmDTO>.CreateSuccessResult(consulta));
        });
    }

    [HttpGet("conversas")]
    public async Task<IActionResult> ListarConversas([FromQuery] DateOnly? data_inicial, [FromQuery] DateOnly? data_final,
        [FromQuery] string? usuario, [FromQuery] string? busca, [FromQuery] int? pagina, [FromQuery] int? tamanho,
        CancellationToken ct)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var conversas = await _auditoriaConversas.ListarAsync(new FiltroConversasChat {
                DataInicial = data_inicial,
                DataFinal = data_final,
                Usuario = usuario,
                Busca = busca,
                Pagina = pagina ?? 1,
                Tamanho = tamanho ?? 30,
            }, ct);
            return Ok(ApiResultDTO<PaginaDTO<ConversaChatResumoDTO>>.CreateSuccessResult(conversas));
        });
    }

    [HttpGet("conversas/{chave:guid}")]
    public async Task<IActionResult> ObterConversa(Guid chave, CancellationToken ct)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var conversa = await _auditoriaConversas.ObterAsync(chave, ct);
            return conversa is null
                ? NotFound(ApiResultDTO<ConversaChatDTO>.CreateFailureResult("Conversa não encontrada"))
                : Ok(ApiResultDTO<ConversaChatDTO>.CreateSuccessResult(conversa));
        });
    }
}
