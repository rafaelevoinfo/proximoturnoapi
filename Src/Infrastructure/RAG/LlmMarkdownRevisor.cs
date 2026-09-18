using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Passa o manual extraído por um modelo barato atrás de erro de leitura do OCR. O modelo
/// só sugere: o que entra no texto é decidido pelas travas de <see cref="RevisaoMarkdown"/>.
/// </summary>
public class LlmMarkdownRevisor(ILogger<LlmMarkdownRevisor> _logger,
                                [FromKeyedServices(LlmMarkdownRevisor.ChaveChat)] IChatClient _chatClient) : IRevisorMarkdown {

    public const string ChaveChat = "revisor";

    private const string Instrucoes = @"Você revisa trechos de manuais de jogos de tabuleiro transcritos de PDF por OCR.
Aponte apenas erros de leitura ou digitação: letras trocadas, faltando ou sobrando que formam uma palavra errada ou fora de contexto (ex.: ""mudos de jogo"" -> ""modos de jogo"").
Não altere números, nomes próprios, nomes de cartas, peças, modos ou termos inventados pelo jogo, regionalismos, gírias, pontuação nem formatação markdown.
Não reescreva frases nem melhore o estilo. Na dúvida, não corrija.
Jogo: {0}. Manual: {1}.
Responda somente com JSON no formato {{""correcoes"":[{{""original"":""trecho exato como está no texto"",""corrigido"":""trecho corrigido"",""motivo"":""curto""}}]}}.
Se não houver erros, responda {{""correcoes"":[]}}.";

    public async Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken cancellationToken) {
        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);
        var texto = new StringBuilder(markdown.Length);
        var aplicadas = 0;
        var descartadas = 0;
        var revisados = 0;
        var falhou = false;

        foreach (var bloco in blocos) {
            var correcoes = await PedirCorrecoesAsync(bloco, contexto, cancellationToken);
            if (correcoes is null) {
                // Bloco sem revisao segue como veio: melhor um trecho nao revisado do que
                // perder o manual inteiro por causa de uma chamada que falhou.
                falhou = true;
                texto.Append(bloco);
                continue;
            }

            revisados++;
            var resultado = RevisaoMarkdown.ValidarEAplicar(bloco, correcoes, markdown, contexto);
            texto.Append(resultado.Texto);
            aplicadas += resultado.Aplicadas.Count;
            descartadas += resultado.Descartadas.Count;

            foreach (var correcao in resultado.Aplicadas) {
                _logger.LogInformation("Revisão aplicou '{Original}' -> '{Corrigido}' ({Motivo}).",
                                       correcao.Original, correcao.Corrigido, correcao.Motivo);
            }

            foreach (var (correcao, motivo) in resultado.Descartadas) {
                _logger.LogDebug("Revisão descartou '{Original}' -> '{Corrigido}': {Motivo}.",
                                 correcao.Original, correcao.Corrigido, motivo);
            }
        }

        _logger.LogInformation("Revisão de {Blocos} bloco(s): {Revisados} revisado(s), {Aplicadas} correção(ões) aplicada(s), {Descartadas} descartada(s).",
                               blocos.Count, revisados, aplicadas, descartadas);

        return new ResultadoRevisao(
            texto.ToString(),
            revisados > 0 ? IAModel.REVISOR_MODEL : null,
            aplicadas,
            descartadas,
            revisados > 0 && !falhou);
    }

    /// <summary>
    /// Uma chamada por bloco. Devolve null quando a resposta não pôde ser aproveitada.
    /// </summary>
    private async Task<IReadOnlyList<CorrecaoRevisao>?> PedirCorrecoesAsync(string bloco, ContextoManual contexto, CancellationToken cancellationToken) {
        try {
            var opcoes = new ChatOptions {
                Instructions = string.Format(Instrucoes, contexto.NomeJogo, contexto.TituloManual),
                // Revisao nao se beneficia de diversidade: cada desvio e uma correcao inventada.
                Temperature = 0f,
                ResponseFormat = ChatResponseFormat.Json,
            };

            var resposta = await _chatClient.GetResponseAsync(new ChatMessage(ChatRole.User, bloco), opcoes, cancellationToken);
            return Interpretar(resposta.Text);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Falha ao revisar um bloco do manual: {Mensagem}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Lê a lista de correções da resposta. Alguns provedores devolvem o JSON com texto em
    /// volta, então o primeiro objeto encontrado também serve.
    /// </summary>
    public static IReadOnlyList<CorrecaoRevisao>? Interpretar(string? resposta) {
        if (string.IsNullOrWhiteSpace(resposta)) {
            return null;
        }

        var inicio = resposta.IndexOf('{');
        var fim = resposta.LastIndexOf('}');
        if (inicio < 0 || fim <= inicio) {
            return null;
        }

        try {
            using var documento = JsonDocument.Parse(resposta[inicio..(fim + 1)]);
            if (!documento.RootElement.TryGetProperty("correcoes", out var lista) || lista.ValueKind != JsonValueKind.Array) {
                return null;
            }

            var correcoes = new List<CorrecaoRevisao>();
            foreach (var item in lista.EnumerateArray()) {
                var original = item.TryGetProperty("original", out var o) ? o.GetString() : null;
                var corrigido = item.TryGetProperty("corrigido", out var c) ? c.GetString() : null;
                var motivo = item.TryGetProperty("motivo", out var m) ? m.GetString() : null;

                if (!string.IsNullOrEmpty(original) && corrigido is not null) {
                    correcoes.Add(new CorrecaoRevisao(original, corrigido, motivo ?? ""));
                }
            }

            return correcoes;
        } catch (JsonException) {
            return null;
        }
    }
}
