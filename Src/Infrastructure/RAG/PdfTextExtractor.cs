using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.RAG;

public class PdfTextExtractor(ILogger<PdfTextExtractor> _logger, IFabricaOpenRouter _fabrica) : ITextExtractor {

    // Acima de ConfiabilidadeAceitavel a extracao e aceita e paramos de gastar modelo.
    // Ate ConfiabilidadeBaixa o modelo atual foi mal demais para este arquivo: o proximo
    // da fila dificilmente resolve, entao pulamos direto para o melhor (e mais caro).
    private const int ConfiabilidadeAceitavel = 80;
    private const int ConfiabilidadeBaixa = 50;

    private const string Instrucoes = @"Você é um assistente de IA especializado em extrair texto de PDFs de manuais de jogos de tabuleiro.
Sua tarefa é ler o conteúdo do arquivo PDF fornecido e retornar o texto extraído em formato markdown.
Certifique-se de manter a formatação básica, como titulo, sub-titulos, parágrafos e listas, sempre que possível.
Transcreva o manual inteiro, do começo ao fim, incluindo apêndices, anexos, glossários, perguntas frequentes, variantes, modos de jogo, exemplos de jogada, resumos de regras, referências e descrições de cartas, peças, ações ou habilidades, e tabelas. Essas partes também são regras e não podem ficar de fora.
Deixe de fora apenas a capa, o sumário (lista de páginas), os créditos e propaganda de outros produtos.
Se houver imagens ou gráficos, descreva-os brevemente no texto extraído.
Não resuma nem corte trechos. Se uma parte estiver ilegível, escreva [trecho ilegível] no lugar dela e continue.
Você pode receber só algumas páginas do manual. Nesse caso transcreva somente essas páginas, por inteiro, sem repetir nem inventar o que vem antes ou depois; se elas começarem no meio de uma seção, continue o texto sem criar um título novo.
Responda APENAS com o markdown do manual, sem cercas de código envolvendo a resposta inteira e sem comentários seus.
Na última linha da resposta, e somente nela, informe uma nota de confiabilidade no formato exato:
<!--CONFIABILIDADE: NN-->
onde NN é um inteiro de 0 a 100 indicando o quanto o texto extraído está completo e coerente com o conteúdo do PDF.
Seja rigoroso: se páginas ficaram de fora ou trechos ficaram ilegíveis, a nota deve refletir isso.";

    public sealed record ExtracaoManual(string Texto, int Confiabilidade);

    // Cauda onde a sentinela e procurada. Folga generosa para o caso de o modelo
    // acrescentar algo depois dela; errar por falta so faria escalar de modelo a toa.
    private const int TamanhoJanelaSentinela = 2048;

    // Folga para manuais longos: o modelo precisa ler o PDF inteiro antes do primeiro token.
    private static readonly TimeSpan TimeoutRede = TimeSpan.FromMinutes(10);

    // RightToLeft para pegar a ultima ocorrencia: se o modelo devolver a resposta dentro
    // de uma cerca de codigo, a sentinela nao fica exatamente no fim do texto.
    private static readonly Regex ConfiabilidadeRegex =
        new(@"<!--\s*CONFIABILIDADE:\s*(\d{1,3})\s*-->", RegexOptions.Compiled | RegexOptions.RightToLeft);

    /// <summary>
    /// Páginas por chamada. Pedir o manual inteiro numa resposta só fazia o modelo resumir
    /// sem avisar: pulava exemplos, tabelas e apêndices e ainda se dava nota alta. Com poucas
    /// páginas por vez a resposta é curta e não há o que encurtar.
    /// </summary>
    public const int PaginasPorBloco = 4;

    // Blocos extraídos ao mesmo tempo: o bastante para um manual de 40 páginas não levar
    // dez chamadas em fila, pouco o bastante para não esbarrar no limite de taxa.
    private const int BlocosEmParalelo = 3;

    // Quatro páginas densas dão uns 6 mil tokens de markdown; o teto só corta resposta que degenerou.
    private const int MaximoTokensBloco = 16000;

    private static readonly TimeSpan TimeoutBloco = TimeSpan.FromMinutes(4);

    /// <summary>Páginas de um bloco, base 1 e inclusivas.</summary>
    public sealed record Bloco(int Primeira, int Ultima) {
        public int Paginas => Ultima - Primeira + 1;
    }

    /// <summary>O que um bloco rendeu: texto, quem extraiu e a nota usada para aceitar.</summary>
    public sealed record ResultadoBloco(string Texto, string Modelo, int Nota, double? Cobertura);

    public async Task<ResultadoExtracao> ExtractTextAsync(string pdfFilePath, CancellationToken cancellationToken) {
        var modelos = IAModel.OCR_MODELS;
        if (modelos.Length == 0) {
            throw new InvalidOperationException("Nenhum modelo de OCR configurado em IAModel.OCR_MODELS.");
        }

        var bytes = await File.ReadAllBytesAsync(pdfFilePath, cancellationToken);
        using var pdf = DocumentoPdf.Abrir(bytes);

        // Sem conseguir abrir ou dividir o PDF não há como ir por partes nem medir: vai inteiro,
        // como antes da divisão, e vale a nota do próprio modelo.
        async Task<ResultadoExtracao> DocumentoInteiroAsync(string motivo) {
            _logger.LogWarning("{Motivo} {PdfFilePath}; extraindo o documento inteiro de uma vez.", motivo, pdfFilePath);
            var inteiro = await ExtrairBlocoAsync(modelos, bytes, [], "Extraia o texto deste PDF em formato markdown",
                                                  TimeoutRede, maximoTokens: null, pdfFilePath, cancellationToken);
            return new ResultadoExtracao(inteiro.Texto, inteiro.Modelo, inteiro.Nota);
        }

        if (pdf is null) {
            return await DocumentoInteiroAsync("Não foi possível ler as páginas de");
        }

        var blocos = DividirEmBlocos(pdf.TotalPaginas, PaginasPorBloco);
        var resultados = new ResultadoBloco[blocos.Count];

        // Trechos e texto de referência saem antes, em sequência: o PdfDocument do PdfPig lê o
        // arquivo sob demanda e não é seguro entre threads. Só as chamadas ao modelo vão em paralelo.
        List<(byte[] Pdf, IReadOnlyList<string> Referencia)> preparados;
        try {
            preparados = blocos
                .Select(b => (pdf.Trecho(b.Primeira, b.Ultima), pdf.Palavras(b.Primeira, b.Ultima)))
                .ToList();
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Falha ao dividir {PdfFilePath} em blocos de páginas.", pdfFilePath);
            return await DocumentoInteiroAsync("Não foi possível dividir");
        }

        // Um bloco que falha derruba os outros: manual com páginas faltando é exatamente o
        // defeito que a divisão existe para evitar, então não vale gravá-lo.
        using var cancelamento = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var vagas = new SemaphoreSlim(BlocosEmParalelo);

        await Task.WhenAll(blocos.Select(async (bloco, i) => {
            await vagas.WaitAsync(cancelamento.Token);
            try {
                var descricao = $"{pdfFilePath} (páginas {bloco.Primeira}-{bloco.Ultima} de {pdf.TotalPaginas})";
                var mensagem = blocos.Count == 1
                    ? "Extraia o texto deste PDF em formato markdown"
                    : $"Estas são as páginas {bloco.Primeira} a {bloco.Ultima} de um manual de {pdf.TotalPaginas} páginas. Transcreva-as integralmente em markdown.";
                resultados[i] = await ExtrairBlocoAsync(modelos, preparados[i].Pdf, preparados[i].Referencia, mensagem,
                                                        TimeoutBloco, MaximoTokensBloco, descricao, cancelamento.Token);
            } catch {
                await cancelamento.CancelAsync();
                throw;
            } finally {
                vagas.Release();
            }
        }));

        var resultado = Combinar(blocos, resultados);
        _logger.LogInformation(
            "Extração de {PdfFilePath}: {Paginas} páginas em {Blocos} bloco(s), confiabilidade {Confiabilidade}, modelo(s) {Modelos}.",
            pdfFilePath, pdf.TotalPaginas, blocos.Count, resultado.Confiabilidade, resultado.Modelo);
        return resultado;
    }

    public static IReadOnlyList<Bloco> DividirEmBlocos(int totalPaginas, int paginasPorBloco) {
        var blocos = new List<Bloco>();
        for (var primeira = 1; primeira <= totalPaginas; primeira += paginasPorBloco) {
            blocos.Add(new Bloco(primeira, Math.Min(primeira + paginasPorBloco - 1, totalPaginas)));
        }
        return blocos;
    }

    /// <summary>
    /// Junta os blocos na ordem das páginas. A confiabilidade é a média das notas pesada pelo
    /// número de páginas; o modelo lista todos os que contribuíram, na ordem da cascata.
    /// </summary>
    public static ResultadoExtracao Combinar(IReadOnlyList<Bloco> blocos, IReadOnlyList<ResultadoBloco> resultados) {
        var texto = string.Join("\n\n", resultados.Select(r => r.Texto.Trim()));
        var paginas = blocos.Sum(b => b.Paginas);
        var confiabilidade = (int)Math.Round(blocos.Zip(resultados, (b, r) => (double)r.Nota * b.Paginas).Sum() / paginas);
        var modelos = string.Join(" + ", resultados.Select(r => r.Modelo).Distinct()
            .OrderBy(m => Array.IndexOf(IAModel.OCR_MODELS, m) is var i and >= 0 ? i : int.MaxValue));
        // A coluna MODELO_EXTRACAO tem 100 caracteres.
        return new ResultadoExtracao(texto, modelos.Length <= 100 ? modelos : modelos[..100], confiabilidade);
    }

    /// <summary>
    /// A cascata de modelos para um bloco. A nota de cada tentativa é a cobertura do texto
    /// embutido nas páginas quando dá para medir, e a do próprio modelo só quando não dá.
    /// </summary>
    private async Task<ResultadoBloco> ExtrairBlocoAsync(string[] modelos, byte[] pdf, IReadOnlyList<string> referencia,
        string mensagem, TimeSpan timeout, int? maximoTokens, string descricao, CancellationToken cancellationToken) {

        // Uma tentativa por modelo: a cascata ja e a nossa retentativa, e o padrao do SDK sao
        // 4 tentativas - o que multiplicaria as chamadas pagas por bloco.
        var clientes = modelos.Select(modelo => _fabrica.CriarChat(modelo, OperacaoLlm.Ocr, timeout, tentativas: 1)).ToArray();
        var chatOptions = new ChatOptions() {
            Instructions = Instrucoes,
            // Extracao e transcricao: nao ha ganho em diversidade, e cada desvio do token mais
            // provavel e uma palavra inventada. Zero tambem estabiliza a nota de confiabilidade.
            Temperature = 0f,
            MaxOutputTokens = maximoTokens,
        };
        var conteudoPdf = new DataContent(pdf, "application/pdf");

        ResultadoBloco? melhor = null;
        var indice = 0;

        while (indice < modelos.Length) {
            var modelo = modelos[indice];
            var extracao = await TentarExtrairAsync(clientes[indice], modelo, conteudoPdf, chatOptions, mensagem, descricao, cancellationToken);
            var resultado = extracao is null ? null : Avaliar(extracao, referencia, modelo);

            if (resultado is not null && (melhor is null || resultado.Nota > melhor.Nota)) {
                melhor = resultado;
            }

            if (resultado is not null && resultado.Nota > ConfiabilidadeAceitavel) {
                break;
            }

            var proximoIndice = ProximoModelo(indice, resultado is null ? null : new ExtracaoManual(resultado.Texto, resultado.Nota), modelos.Length);
            if (proximoIndice == indice) {
                break;
            }

            _logger.LogWarning(
                "Nota {Nota} insuficiente para {Descricao} com o modelo {Modelo} (cobertura do texto do PDF: {Cobertura}). Escalando para {ProximoModelo}.",
                resultado?.Nota ?? 0, descricao, modelo, resultado?.Cobertura?.ToString("P0") ?? "não medida", modelos[proximoIndice]);

            indice = proximoIndice;
        }

        if (melhor is null) {
            throw new InvalidOperationException($"Nenhum modelo conseguiu extrair o texto de {descricao}.");
        }

        if (melhor.Nota <= ConfiabilidadeAceitavel) {
            _logger.LogWarning("Todos os modelos ficaram abaixo do aceitável para {Descricao}. Melhor resultado: {Modelo} com nota {Nota}.",
                               descricao, melhor.Modelo, melhor.Nota);
        }

        return melhor;
    }

    /// <summary>Nota da tentativa: cobertura medida quando a página tem texto embutido, senão a do modelo.</summary>
    public static ResultadoBloco Avaliar(ExtracaoManual extracao, IReadOnlyList<string> referencia, string modelo) {
        var cobertura = CoberturaTexto.Calcular(referencia, extracao.Texto);
        var nota = cobertura is { } c ? (int)Math.Round(c * 100) : extracao.Confiabilidade;
        return new ResultadoBloco(extracao.Texto, modelo, nota, cobertura);
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
        IChatClient chatClient,
        string modelo,
        DataContent conteudoPdf,
        ChatOptions chatOptions,
        string mensagem,
        string pdfFilePath,
        CancellationToken cancellationToken) {

        try {
            _logger.LogDebug("Extraindo texto de {PdfFilePath} com o modelo {Modelo}.", pdfFilePath, modelo);

            var message = new ChatMessage(ChatRole.User, mensagem);
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
