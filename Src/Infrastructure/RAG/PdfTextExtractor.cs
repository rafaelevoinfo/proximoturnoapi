using System.ClientModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using OpenAI;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.RAG;

public class PdfTextExtractor(ILogger<PdfTextExtractor> _logger) : ITextExtractor {

    // Acima de ConfiabilidadeAceitavel a extracao e aceita e paramos de gastar modelo.
    // Ate ConfiabilidadeBaixa o modelo atual foi mal demais para este arquivo: o proximo
    // da fila dificilmente resolve, entao pulamos direto para o melhor (e mais caro).
    private const int ConfiabilidadeAceitavel = 80;
    private const int ConfiabilidadeBaixa = 50;

    private const string Instrucoes = @"Você é um assistente de IA especializado em extrair texto de PDFs de manuais de jogos de tabuleiro.
Sua tarefa é ler o conteúdo do arquivo PDF fornecido e retornar o texto extraído em formato markdown.
Certifique-se de manter a formatação básica, como titulo, sub-titulos, parágrafos e listas, sempre que possível.
Ignore textos de capa, indices, creditos ou qualquer informação irrelevante para as regras do jogos.
Se houver imagens ou gráficos, descreva-os brevemente no texto extraído.
Apos realizar a extração, valide se alguma parte do texto ficou sem sentido ou incompleta, caso sim, remova-a.
Responda APENAS com o markdown do manual, sem cercas de código envolvendo a resposta inteira e sem comentários seus.
Na última linha da resposta, e somente nela, informe uma nota de confiabilidade no formato exato:
<!--CONFIABILIDADE: NN-->
onde NN é um inteiro de 0 a 100 indicando o quanto o texto extraído está completo e coerente com o conteúdo do PDF.
Seja rigoroso: se páginas ficaram de fora ou trechos ficaram ilegíveis, a nota deve refletir isso.";

    public sealed record ExtracaoManual(string Texto, int Confiabilidade);

    // Cauda onde a sentinela e procurada. Folga generosa para o caso de o modelo
    // acrescentar algo depois dela; errar por falta so faria escalar de modelo a toa.
    private const int TamanhoJanelaSentinela = 2048;

    // RightToLeft para pegar a ultima ocorrencia: se o modelo devolver a resposta dentro
    // de uma cerca de codigo, a sentinela nao fica exatamente no fim do texto.
    private static readonly Regex ConfiabilidadeRegex =
        new(@"<!--\s*CONFIABILIDADE:\s*(\d{1,3})\s*-->", RegexOptions.Compiled | RegexOptions.RightToLeft);

    public async Task<string> ExtractTextAsync(string pdfFilePath, CancellationToken cancellationToken) {
        var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(openRouterApiKey)) {
            throw new InvalidOperationException("OPENROUTER_API_KEY não configurada.");
        }

        var modelos = IAModel.OCR_MODELS;
        if (modelos.Length == 0) {
            throw new InvalidOperationException("Nenhum modelo de OCR configurado em IAModel.OCR_MODELS.");
        }

        var openAiClient = new OpenAIClient(new ApiKeyCredential(openRouterApiKey), new OpenAIClientOptions() {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
        });

        var chatOptions = new ChatOptions() {
            Instructions = Instrucoes,
            // Extracao e transcricao: nao ha ganho em diversidade, e cada desvio do token mais
            // provavel e uma palavra inventada. Zero tambem estabiliza a nota de confiabilidade.
            Temperature = 0f,
        };

        // O PDF é lido uma única vez e reaproveitado em todas as tentativas.
        var conteudoPdf = await DataContent.LoadFromAsync(pdfFilePath, "application/pdf", cancellationToken);

        ExtracaoManual? melhorExtracao = null;
        string? melhorModelo = null;
        var indice = 0;

        while (indice < modelos.Length) {
            var modelo = modelos[indice];
            var extracao = await TentarExtrairAsync(openAiClient, modelo, conteudoPdf, chatOptions, pdfFilePath, cancellationToken);

            if (extracao is not null && (melhorExtracao is null || extracao.Confiabilidade > melhorExtracao.Confiabilidade)) {
                melhorExtracao = extracao;
                melhorModelo = modelo;
            }

            if (extracao is not null && extracao.Confiabilidade > ConfiabilidadeAceitavel) {
                _logger.LogInformation(
                    "Extração de {PdfFilePath} aceita com o modelo {Modelo} (confiabilidade {Confiabilidade}).",
                    pdfFilePath, modelo, extracao.Confiabilidade);
                break;
            }

            var proximoIndice = ProximoModelo(indice, extracao, modelos.Length);
            if (proximoIndice == indice) {
                break;
            }

            _logger.LogWarning(
                "Confiabilidade {Confiabilidade} insuficiente para {PdfFilePath} com o modelo {Modelo}. Escalando para {ProximoModelo}.",
                extracao?.Confiabilidade ?? 0, pdfFilePath, modelo, modelos[proximoIndice]);

            indice = proximoIndice;
        }

        if (melhorExtracao is null) {
            throw new InvalidOperationException($"Nenhum modelo conseguiu extrair o texto de {pdfFilePath}.");
        }

        if (melhorExtracao.Confiabilidade <= ConfiabilidadeAceitavel) {
            _logger.LogWarning(
                "Todos os modelos ficaram abaixo do aceitável para {PdfFilePath}. Melhor resultado: {Modelo} com confiabilidade {Confiabilidade}.",
                pdfFilePath, melhorModelo, melhorExtracao.Confiabilidade);
        }

        await File.WriteAllTextAsync(Path.ChangeExtension(pdfFilePath, ".md"), melhorExtracao.Texto, cancellationToken);
        return melhorExtracao.Texto;
    }

    /// <summary>
    /// Política de escalonamento. Isolada do I/O para poder ser testada sem chamar a API.
    /// Devolve o próprio índice quando não há mais nada a tentar.
    /// </summary>
    public static int ProximoModelo(int indiceAtual, ExtracaoManual? extracao, int totalModelos) {
        var ultimo = totalModelos - 1;
        if (indiceAtual >= ultimo) {
            return indiceAtual;
        }

        return extracao?.Confiabilidade <= ConfiabilidadeBaixa ? ultimo : indiceAtual + 1;
    }

    /// <summary>
    /// Separa o markdown da sentinela de confiabilidade. Sem sentinela a nota vira 0:
    /// o texto continua sendo um candidato, mas so vence se nenhum modelo fizer melhor.
    /// </summary>
    public static ExtracaoManual? Interpretar(string? resposta) {
        if (string.IsNullOrWhiteSpace(resposta)) {
            return null;
        }

        // A sentinela e pedida na ultima linha, entao so a cauda interessa. Isso torna o
        // custo do parse independente do tamanho do manual e impede que algo parecido no
        // corpo do documento seja lido como nota.
        var inicioJanela = Math.Max(0, resposta.Length - TamanhoJanelaSentinela);
        var match = ConfiabilidadeRegex.Match(resposta, inicioJanela, resposta.Length - inicioJanela);
        if (!match.Success) {
            return new ExtracaoManual(resposta.Trim(), 0);
        }

        var texto = (resposta[..match.Index] + resposta[(match.Index + match.Length)..]).Trim();
        if (texto.Length == 0) {
            return null;
        }

        return new ExtracaoManual(texto, Math.Clamp(int.Parse(match.Groups[1].Value), 0, 100));
    }

    /// <summary>
    /// Uma tentativa contra um modelo. Devolve null quando a tentativa não pode ser aproveitada,
    /// para que a falha de um modelo não derrube a cadeia inteira.
    /// </summary>
    private async Task<ExtracaoManual?> TentarExtrairAsync(
        OpenAIClient openAiClient,
        string modelo,
        DataContent conteudoPdf,
        ChatOptions chatOptions,
        string pdfFilePath,
        CancellationToken cancellationToken) {

        try {
            _logger.LogDebug("Extraindo texto de {PdfFilePath} com o modelo {Modelo}.", pdfFilePath, modelo);

            var chatClient = openAiClient.GetChatClient(modelo).AsIChatClient();

            var message = new ChatMessage(ChatRole.User, "Extraia o texto deste PDF em formato markdown");
            message.Contents.Add(conteudoPdf);

            var response = await chatClient.GetResponseAsync(message, chatOptions, cancellationToken);

            // Resposta truncada: o manual esta incompleto e a sentinela, que vem no fim,
            // nem chegou a ser escrita. Nao aproveitamos um texto sabidamente cortado.
            if (response.FinishReason == ChatFinishReason.Length) {
                _logger.LogWarning("Resposta do modelo {Modelo} para {PdfFilePath} foi truncada por limite de tokens.", modelo, pdfFilePath);
                return null;
            }

            var extracao = Interpretar(response.Text);
            if (extracao is null) {
                _logger.LogWarning("Modelo {Modelo} não retornou texto para {PdfFilePath}.", modelo, pdfFilePath);
                return null;
            }

            if (extracao.Confiabilidade == 0) {
                _logger.LogWarning("Modelo {Modelo} não informou a confiabilidade para {PdfFilePath}. Tratando como nota zero.", modelo, pdfFilePath);
            }

            return extracao;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _logger.LogError(ex, "Falha ao extrair texto de {PdfFilePath} com o modelo {Modelo}: {Message}", pdfFilePath, modelo, ex.Message);
            return null;
        }
    }
}
