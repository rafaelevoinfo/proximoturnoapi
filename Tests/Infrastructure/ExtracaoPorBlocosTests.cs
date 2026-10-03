using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.RAG;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class ExtracaoPorBlocosTests : IDisposable {

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "extracao-" + Guid.NewGuid().ToString("N"));

    public ExtracaoPorBlocosTests() => Directory.CreateDirectory(_pasta);

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static string Gemini => IAModel.OCR_MODELS[0];
    private static string Opus => IAModel.OCR_MODELS[^1];

    // Palavras únicas por página: "regra3x7" é a 7ª palavra da página 3.
    private static IEnumerable<string> PalavrasDaPagina(int pagina, int quantidade) =>
        Enumerable.Range(1, quantidade).Select(i => $"regra{pagina}x{i}");

    /// <summary>PDF com texto embutido, <paramref name="palavrasPorPagina"/> palavras em cada página.</summary>
    private static byte[] CriarPdf(int paginas, int palavrasPorPagina) {
        var construtor = new PdfDocumentBuilder();
        var fonte = construtor.AddStandard14Font(Standard14Font.Helvetica);
        for (var p = 1; p <= paginas; p++) {
            var pagina = construtor.AddPage(PageSize.A4);
            var linhas = PalavrasDaPagina(p, palavrasPorPagina).Chunk(8).ToList();
            for (var l = 0; l < linhas.Count; l++) {
                pagina.AddText(string.Join(' ', linhas[l]), 10, new PdfPoint(40, 780 - l * 14), fonte);
            }
        }
        return construtor.Build();
    }

    private string Salvar(byte[] pdf) {
        var caminho = Path.Combine(_pasta, "manual.pdf");
        File.WriteAllBytes(caminho, pdf);
        return caminho;
    }

    /// <summary>
    /// "Modelo" que lê as páginas do PDF recebido e devolve a transcrição. Por padrão é
    /// perfeito; <see cref="Pular"/> faz ele omitir páginas, como os modelos fazem de verdade.
    /// </summary>
    private sealed class ModeloFalso(string nome, int notaAutodeclarada = 95) : IChatClient {
        public ConcurrentBag<(int Paginas, string Mensagem, int? MaximoTokens)> Chamadas { get; } = [];
        public Func<int, bool> Pular { get; set; } = _ => false;
        public bool Falhar { get; set; }
        private int _tentativas;
        public int Tentativas => _tentativas;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref _tentativas);
            var mensagem = messages.Single();
            var pdf = mensagem.Contents.OfType<DataContent>().Single();
            using var documento = PdfDocument.Open(pdf.Data.ToArray());
            Chamadas.Add((documento.NumberOfPages, mensagem.Text, options?.MaxOutputTokens));
            if (Falhar) {
                throw new HttpRequestException($"{nome} fora do ar");
            }

            var texto = string.Join("\n\n", documento.GetPages()
                .Select(p => string.Join(' ', p.GetWords().Select(w => w.Text)))
                .Where(t => !Pular(int.Parse(t.Split(' ')[0]["regra".Length..].Split('x')[0]))));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"{texto}\n<!--CONFIABILIDADE: {notaAutodeclarada}-->")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FabricaFalsa(Dictionary<string, ModeloFalso> modelos) : IFabricaOpenRouter {
        public IChatClient CriarChat(string modelo, OperacaoLlm operacao, TimeSpan timeout, int tentativas) => modelos[modelo];

        public IEmbeddingGenerator<string, Embedding<float>> CriarEmbedding(string modelo, OperacaoLlm operacao = OperacaoLlm.Embedding) =>
            throw new NotImplementedException();
    }

    private static (PdfTextExtractor Extrator, ModeloFalso Gemini, ModeloFalso Opus) Extrator(int notaGemini = 95) {
        var gemini = new ModeloFalso("gemini", notaGemini);
        var opus = new ModeloFalso("opus");
        var fabrica = new FabricaFalsa(new() { [Gemini] = gemini, [Opus] = opus });
        return (new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance, fabrica), gemini, opus);
    }

    // ---- extração completa ----

    [Fact]
    public async Task ManualLongo_ExtraiPorBlocos_EJuntaNaOrdem() {
        var (extrator, gemini, opus) = Extrator();
        var caminho = Salvar(CriarPdf(paginas: 10, palavrasPorPagina: 15));

        var resultado = await extrator.ExtractTextAsync(caminho, CancellationToken.None);

        // 10 páginas em blocos de 4: 1-4, 5-8, 9-10.
        Assert.Equal([2, 4, 4], gemini.Chamadas.Select(c => c.Paginas).Order());
        Assert.Empty(opus.Chamadas);
        Assert.All(gemini.Chamadas, c => Assert.NotNull(c.MaximoTokens));
        Assert.Contains(gemini.Chamadas, c => c.Mensagem.Contains("páginas 5 a 8 de um manual de 10 páginas"));

        var posicoes = Enumerable.Range(1, 10).Select(p => resultado.Texto.IndexOf($"regra{p}x1 ", StringComparison.Ordinal)).ToList();
        Assert.All(posicoes, p => Assert.True(p >= 0));
        Assert.Equal(posicoes.Order(), posicoes);
        Assert.Equal(Gemini, resultado.Modelo);
        // Blocos 1-4 e 5-8 medidos (100); 9-10 tem texto de menos e fica com a nota do modelo (95).
        Assert.Equal(99, resultado.Confiabilidade);
    }

    [Fact]
    public async Task BlocoComPaginaOmitida_EhRefeitoNoModeloMaisForte() {
        var (extrator, gemini, opus) = Extrator(notaGemini: 98);
        // O Gemini "pula" a página 6 e mesmo assim se dá 98: só a cobertura percebe.
        gemini.Pular = pagina => pagina == 6;
        var caminho = Salvar(CriarPdf(paginas: 10, palavrasPorPagina: 15));

        var resultado = await extrator.ExtractTextAsync(caminho, CancellationToken.None);

        var refeito = Assert.Single(opus.Chamadas);
        Assert.Contains("páginas 5 a 8", refeito.Mensagem);
        Assert.Contains("regra6x1 ", resultado.Texto);
        Assert.Equal($"{Gemini} + {Opus}", resultado.Modelo);
    }

    [Fact]
    public async Task PaginasSemTextoEmbutido_UsamANotaDoModelo() {
        // 9 palavras por página: nenhum bloco chega ao mínimo para medir cobertura.
        var (extrator, gemini, opus) = Extrator(notaGemini: 60);
        var caminho = Salvar(CriarPdf(paginas: 4, palavrasPorPagina: 9));

        var resultado = await extrator.ExtractTextAsync(caminho, CancellationToken.None);

        // Nota 60 do próprio modelo (entre 51 e 80) escala para o próximo da fila.
        Assert.Single(gemini.Chamadas);
        Assert.Single(opus.Chamadas);
        Assert.Equal(95, resultado.Confiabilidade);
        Assert.Equal(Opus, resultado.Modelo);
    }

    [Fact]
    public async Task ManualCurto_VaiNumaChamadaSo_ComOPdfOriginal() {
        var (extrator, gemini, _) = Extrator();
        var caminho = Salvar(CriarPdf(paginas: 3, palavrasPorPagina: 20));

        await extrator.ExtractTextAsync(caminho, CancellationToken.None);

        var chamada = Assert.Single(gemini.Chamadas);
        Assert.Equal(3, chamada.Paginas);
        Assert.Equal("Extraia o texto deste PDF em formato markdown", chamada.Mensagem);
    }

    [Fact]
    public async Task BlocoQueNenhumModeloExtrai_FalhaOManualInteiro() {
        var (extrator, gemini, opus) = Extrator();
        gemini.Falhar = true;
        opus.Falhar = true;
        var caminho = Salvar(CriarPdf(paginas: 10, palavrasPorPagina: 15));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => extrator.ExtractTextAsync(caminho, CancellationToken.None));
        Assert.Contains("páginas", erro.Message);
    }

    [Fact]
    public async Task ArquivoQueNaoEhPdfLegivel_VaiInteiroComoAntes() {
        var (extrator, gemini, opus) = Extrator();
        var caminho = Path.Combine(_pasta, "estranho.pdf");
        File.WriteAllBytes(caminho, "não é um pdf"u8.ToArray());

        // O modelo falso também não consegue abrir: o que interessa é que a chamada foi feita
        // com o arquivo inteiro e a falha veio dele, não da divisão.
        await Assert.ThrowsAsync<InvalidOperationException>(() => extrator.ExtractTextAsync(caminho, CancellationToken.None));
        Assert.Equal(1, gemini.Tentativas);
        Assert.Equal(1, opus.Tentativas);
    }

    // ---- peças ----

    [Theory]
    [InlineData(1, 4, new[] { 1, 1 })]
    [InlineData(4, 4, new[] { 1, 4 })]
    [InlineData(10, 4, new[] { 1, 4, 5, 8, 9, 10 })]
    public void DividirEmBlocos_CobreTodasAsPaginasSemSobreposicao(int total, int porBloco, int[] limites) {
        var blocos = PdfTextExtractor.DividirEmBlocos(total, porBloco);

        Assert.Equal(limites, blocos.SelectMany(b => new[] { b.Primeira, b.Ultima }));
    }

    [Fact]
    public void Combinar_PesaANotaPeloNumeroDePaginas() {
        var blocos = new[] { new PdfTextExtractor.Bloco(1, 4), new PdfTextExtractor.Bloco(5, 5) };
        var resultados = new[] {
            new PdfTextExtractor.ResultadoBloco("A", Opus, 100, 1.0),
            new PdfTextExtractor.ResultadoBloco("B", Gemini, 50, 0.5),
        };

        var r = PdfTextExtractor.Combinar(blocos, resultados);

        Assert.Equal("A\n\nB", r.Texto);
        Assert.Equal(90, r.Confiabilidade);
        // Na ordem da cascata, não na ordem dos blocos.
        Assert.Equal($"{Gemini} + {Opus}", r.Modelo);
    }

    [Fact]
    public void Cobertura_MedeOQueFaltou_IgnorandoAcentoCaixaELigaduras() {
        var referencia = Enumerable.Range(1, 50).Select(i => $"palavra{i}").Append("Configuração").Append("ﬁcha").ToList();
        var completo = string.Join(' ', Enumerable.Range(1, 50).Select(i => $"PALAVRA{i}")) + " configuracao ficha";
        var metade = string.Join(' ', Enumerable.Range(1, 26).Select(i => $"palavra{i}"));

        Assert.Equal(1.0, CoberturaTexto.Calcular(referencia, completo));
        Assert.Equal(0.5, CoberturaTexto.Calcular(referencia, metade));
    }

    [Fact]
    public void Cobertura_ComPoucoTextoNaReferencia_NaoMede() {
        var referencia = Enumerable.Range(1, CoberturaTexto.MinimoTermosReferencia - 1).Select(i => $"termo{i}").ToList();

        Assert.Null(CoberturaTexto.Calcular(referencia, "qualquer coisa"));
    }

    [Fact]
    public void Palavras_JuntaHifenizacaoDeFimDeLinha() {
        Assert.Equal(["continua", "Fim-", "Jogo", "pré-jogo"],
            DocumentoPdf.JuntarHifenizadas(["con-", "tinua", "Fim-", "Jogo", "pré-jogo"]));
    }

    [Fact]
    public void Trecho_TemSoAsPaginasPedidas() {
        using var pdf = DocumentoPdf.Abrir(CriarPdf(paginas: 6, palavrasPorPagina: 10))!;

        using var trecho = PdfDocument.Open(pdf.Trecho(3, 4));

        Assert.Equal(2, trecho.NumberOfPages);
        Assert.StartsWith("regra3x1", trecho.GetPage(1).GetWords().First().Text);
        Assert.Equal(20, pdf.Palavras(3, 4).Count);
    }

    [Fact]
    public void Abrir_ArquivoInvalido_DevolveNull() =>
        Assert.Null(DocumentoPdf.Abrir("lixo"u8.ToArray()));
}
