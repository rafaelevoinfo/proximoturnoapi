using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.IA;

/// <summary>
/// Lê a resposta de cada tentativa de chamada e entrega uma linha ao ledger. Instalada em
/// <see cref="PipelinePosition.PerTry"/>: uma tentativa que morre do nosso lado pode ter sido
/// concluída e cobrada pelo servidor, e em PerCall esse gasto ficaria invisível.
/// <para>
/// Ela só lê a resposta. A requisição não é lida nem tocada, porque no OCR ela carrega o PDF
/// inteiro em base64.
/// </para>
/// </summary>
public sealed class PoliticaUsoLlm(ILogger<PoliticaUsoLlm> _logger,
                                   IRegistradorUsoLlm _registrador,
                                   string _modelo,
                                   OperacaoLlm _operacao) : PipelinePolicy {

    // Um corpo de erro inteiro nao cabe na coluna e nao acrescenta nada depois do comeco.
    private const int TamanhoTrecho = 200;

    private int _avisouStreaming;

    public override void Process(PipelineMessage mensagem, IReadOnlyList<PipelinePolicy> pipeline, int indice) {
        var relogio = Stopwatch.StartNew();
        try {
            ProcessNext(mensagem, pipeline, indice);
        } catch (OperationCanceledException) {
            // Desligamento: nao ha resposta para ler e o DbContext ja pode estar indo embora.
            throw;
        } catch (Exception excecao) {
            Entregar(mensagem, relogio, excecao);
            throw;
        }

        Entregar(mensagem, relogio, null);
    }

    public override async ValueTask ProcessAsync(PipelineMessage mensagem, IReadOnlyList<PipelinePolicy> pipeline, int indice) {
        var relogio = Stopwatch.StartNew();
        try {
            await ProcessNextAsync(mensagem, pipeline, indice);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception excecao) {
            await EntregarAsync(mensagem, relogio, excecao);
            throw;
        }

        await EntregarAsync(mensagem, relogio, null);
    }

    private void Entregar(PipelineMessage mensagem, Stopwatch relogio, Exception? excecao) {
        var registro = Ler(mensagem, relogio, excecao);
        if (registro is null) {
            return;
        }

        // O contrato do registrador diz que ele nao lanca, mas a policy esta no caminho de
        // uma chamada paga: confiar nisso seria trocar um gasto perdido por uma chamada
        // perdida.
        try {
            _registrador.Registrar(registro);
        } catch (Exception falha) {
            NaoRegistrou(falha);
        }
    }

    private async ValueTask EntregarAsync(PipelineMessage mensagem, Stopwatch relogio, Exception? excecao) {
        var registro = Ler(mensagem, relogio, excecao);
        if (registro is null) {
            return;
        }

        try {
            await _registrador.RegistrarAsync(registro);
        } catch (Exception falha) {
            NaoRegistrou(falha);
        }
    }

    private void NaoRegistrou(Exception falha) =>
        _logger.LogWarning(falha, "Uso de {Modelo} não foi registrado no ledger: {Mensagem}", _modelo, falha.Message);

    /// <summary>
    /// Nunca lança: uma exceção nossa aqui derrubaria uma chamada de LLM que já foi paga.
    /// Devolve null só quando não há o que registrar.
    /// </summary>
    private RegistroUsoLlm? Ler(PipelineMessage mensagem, Stopwatch relogio, Exception? excecao) {
        relogio.Stop();
        var duracao = (int)Math.Min(relogio.ElapsedMilliseconds, int.MaxValue);

        try {
            if (excecao is not null) {
                return Simples(duracao, DesfechoLlm.Excecao, $"{excecao.GetType().Name}: {excecao.Message}");
            }

            var resposta = mensagem.Response;
            if (resposta is null) {
                return Simples(duracao, DesfechoLlm.Excecao, "Sem resposta.");
            }

            if (!mensagem.BufferResponse) {
                // Streaming: o corpo nao esta em memoria e consumi-lo aqui roubaria o do SDK.
                if (Interlocked.Exchange(ref _avisouStreaming, 1) == 0) {
                    _logger.LogDebug("Chamada em streaming para {Modelo} não entra no ledger.", _modelo);
                }

                return null;
            }

            var httpOk = resposta.Status is >= 200 and < 300;
            var corpo = resposta.Content?.ToMemory() ?? ReadOnlyMemory<byte>.Empty;
            if (corpo.IsEmpty) {
                return Simples(duracao, httpOk ? DesfechoLlm.Ok : DesfechoLlm.ErroHttp,
                               httpOk ? null : $"HTTP {resposta.Status}");
            }

            // O JsonDocument aluga buffer do ArrayPool: sem o using, toda chamada vazaria um
            // pedaco do tamanho do manual.
            using var documento = JsonDocument.Parse(corpo);
            var raiz = documento.RootElement;
            var uso = Filho(raiz, "usage");
            var finishReason = FinishReason(raiz);

            return new RegistroUsoLlm(
                _operacao,
                _modelo,
                Texto(raiz, "model"),
                Texto(raiz, "provider"),
                Inteiro(uso, "prompt_tokens"),
                Inteiro(uso, "completion_tokens"),
                Inteiro(Filho(uso, "completion_tokens_details"), "reasoning_tokens"),
                Inteiro(Filho(uso, "prompt_tokens_details"), "cached_tokens"),
                Custo(uso),
                duracao,
                Desfecho(httpOk, finishReason),
                Detalhe(httpOk, resposta.Status, finishReason, corpo),
                Texto(raiz, "id"));
        } catch (Exception falha) {
            _logger.LogDebug(falha, "Não foi possível ler o uso da resposta de {Modelo}.", _modelo);

            var status = mensagem.Response?.Status;
            var httpOk = status is >= 200 and < 300;
            return Simples(duracao, httpOk ? DesfechoLlm.Ok : DesfechoLlm.ErroHttp,
                           httpOk ? "Corpo ilegível." : $"HTTP {status}");
        }
    }

    private RegistroUsoLlm Simples(int duracao, DesfechoLlm desfecho, string? detalhe) =>
        new(_operacao, _modelo, null, null, 0, 0, 0, 0, null, duracao, desfecho, detalhe, null);

    private static DesfechoLlm Desfecho(bool httpOk, string? finishReason) {
        if (!httpOk) {
            return DesfechoLlm.ErroHttp;
        }

        // Embedding nao tem choices, logo nao tem finish_reason: resposta completa.
        return finishReason switch {
            null or "stop" => DesfechoLlm.Ok,
            "length" => DesfechoLlm.Truncado,
            _ => DesfechoLlm.ErroDoModelo
        };
    }

    private static string? Detalhe(bool httpOk, int status, string? finishReason, ReadOnlyMemory<byte> corpo) {
        if (!httpOk) {
            return $"HTTP {status}: {Trecho(corpo)}";
        }

        return finishReason is null or "stop" ? null : finishReason;
    }

    private static string Trecho(ReadOnlyMemory<byte> corpo) {
        var tamanho = Math.Min(TamanhoTrecho, corpo.Length);
        return Encoding.UTF8.GetString(corpo.Span[..tamanho]);
    }

    private static JsonElement Filho(JsonElement pai, string propriedade) =>
        pai.ValueKind == JsonValueKind.Object && pai.TryGetProperty(propriedade, out var valor) ? valor : default;

    private static string? Texto(JsonElement objeto, string propriedade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.TryGetProperty(propriedade, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static int Inteiro(JsonElement objeto, string propriedade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.TryGetProperty(propriedade, out var valor)
        && valor.TryGetInt32(out var numero)
            ? numero
            : 0;

    private static decimal? Custo(JsonElement uso) {
        if (uso.ValueKind != JsonValueKind.Object || !uso.TryGetProperty("cost", out var valor)) {
            return null;
        }

        // Chega como 3.43e-07. Decimal aguenta a notacao e nao perde centavo na soma grande;
        // valor menor que a precisao da coluna vira zero no insert, o que e aceitavel.
        return valor.TryGetDecimal(out var custo) ? custo : null;
    }

    private static string? FinishReason(JsonElement raiz) {
        var escolhas = Filho(raiz, "choices");
        if (escolhas.ValueKind != JsonValueKind.Array || escolhas.GetArrayLength() == 0) {
            return null;
        }

        return Texto(escolhas[0], "finish_reason");
    }
}
