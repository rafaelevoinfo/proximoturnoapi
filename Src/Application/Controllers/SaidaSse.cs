using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.Chat;

namespace ProximoTurnoApi.Application.Controllers;

/// <summary>
/// Escreve a resposta do chat como Server-Sent Events. Os cabeçalhos só vão quando o primeiro
/// evento sai: até lá, o controller ainda pode responder um erro comum em JSON.
/// <para>Eventos: <c>resposta</c> (pronta, sem modelo), <c>inicio</c>, <c>texto</c>, <c>fim</c> e <c>erro</c>.</para>
/// </summary>
public sealed class SaidaSse(HttpResponse _resposta) : ISaidaChat {

    // Os mesmos padroes do MVC (camelCase); os enums do chat ja trazem o conversor de texto.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Algum evento já foi escrito: a partir daqui não dá mais para mudar o status HTTP.</summary>
    public bool Iniciado { get; private set; }

    public Task IniciarAsync(RespostaChatDTO cabecalho, CancellationToken cancellationToken) =>
        EventoAsync("inicio", cabecalho, cancellationToken);

    public Task EscreverAsync(string trecho, CancellationToken cancellationToken) =>
        EventoAsync("texto", new { t = trecho }, cancellationToken);

    public async Task EventoAsync(string nome, object dados, CancellationToken cancellationToken) {
        if (!Iniciado) {
            Iniciado = true;
            _resposta.StatusCode = StatusCodes.Status200OK;
            _resposta.ContentType = "text/event-stream; charset=utf-8";
            _resposta.Headers.CacheControl = "no-cache";
            // nginx e parecidos acumulam a resposta e anulariam o streaming sem este cabecalho.
            _resposta.Headers["X-Accel-Buffering"] = "no";
            _resposta.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        }

        // O JSON nunca tem quebra de linha crua (ela vira \n dentro da string), entao cabe numa
        // unica linha "data:".
        await _resposta.WriteAsync($"event: {nome}\ndata: {JsonSerializer.Serialize(dados, Json)}\n\n", cancellationToken);
        await _resposta.Body.FlushAsync(cancellationToken);
    }
}
