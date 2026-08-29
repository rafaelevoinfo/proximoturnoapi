using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ChunkingExtractorTests {

    /// <summary>
    /// Corpo grande o suficiente para nao acionar a fusao de secoes pequenas,
    /// para que os testes de estrutura vejam uma secao por chunk.
    /// </summary>
    private static string Corpo(string prefixo) =>
        prefixo + " " + string.Join(" ", Enumerable.Repeat("Regra do jogo.", 30));

    /// <summary>
    /// Texto de pouco menos de <paramref name="tamanho"/> caracteres, feito de frases
    /// inteiras. Derivado das constantes para o teste nao perder o sentido se elas mudarem.
    /// </summary>
    private static string Preencher(int tamanho) {
        const string frase = "Uma regra qualquer.";
        return string.Join(" ", Enumerable.Repeat(frase, Math.Max(1, tamanho / (frase.Length + 1))));
    }

    [Fact]
    public void Dividir_GeraUmChunkPorSecaoComCorpo() {
        var markdown = $"""
            # Azul

            ## Preparação

            {Corpo("Cada jogador pega um tabuleiro.")}

            ## Turno

            ### Pegar peças

            {Corpo("Escolha uma fábrica.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        // "Azul" e "Turno" so tem subsecoes: nao viram chunk sozinhos.
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Azul > Preparação", chunks[0].Titulo);
        Assert.Equal("Azul > Turno > Pegar peças", chunks[1].Titulo);
    }

    [Fact]
    public void Dividir_NumeraOsChunksEmSequencia() {
        var markdown = $"""
            # Azul

            ## Preparação

            {Corpo("Monte o tabuleiro.")}

            ## Turno

            {Corpo("Escolha uma fábrica.")}

            ## Pontuação

            {Corpo("Conte os pontos.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Equal([0, 1, 2], chunks.Select(c => c.Ordem));
    }

    [Fact]
    public void Dividir_TituloDeNivelSuperior_DescartaOsAninhadosAnteriores() {
        // Depois de "### Pegar peças", um "## Pontuação" nao pode herdar o nivel 3.
        var markdown = $"""
            # Azul

            ## Turno

            ### Pegar peças

            {Corpo("Escolha uma fábrica.")}

            ## Pontuação

            {Corpo("Conte os pontos.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Equal("Azul > Pontuação", chunks[^1].Titulo);
    }

    [Fact]
    public void Dividir_TextoAntesDoPrimeiroTitulo_ViraChunkSemTitulo() {
        var markdown = $"""
            {Corpo("Bem-vindo ao manual.")}

            # Azul

            {Corpo("Regras gerais.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Equal("", chunks[0].Titulo);
        Assert.StartsWith("Bem-vindo ao manual.", chunks[0].Texto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("# Azul\n\n## Turno\n")]
    public void Dividir_SemConteudoAproveitavel_DevolveListaVazia(string markdown) {
        Assert.Empty(ChunkingExtractor.Dividir(markdown));
    }

    [Fact]
    public void Dividir_SecaoMenorQueOMinimo_FundeNaAnterior() {
        var markdown = $"""
            # Azul

            ## Preparação

            {Corpo("Monte o tabuleiro.")}

            ## Fim do jogo

            O jogo acaba quando alguém completa uma linha horizontal.
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Single(chunks);
        Assert.Equal("Azul > Preparação", chunks[0].Titulo);
        // A linha de titulo da secao fundida e preservada no texto, senao o
        // conteudo se mistura ao da secao anterior sem nenhuma separacao.
        Assert.Contains("## Fim do jogo", chunks[0].Texto);
        Assert.Contains("O jogo acaba quando", chunks[0].Texto);
    }

    [Fact]
    public void Dividir_SecaoPequenaQueNaoCabeNaAnterior_ContinuaSeparada() {
        // Deixa a secao anterior tao cheia que nem o titulo mais o corpo da seguinte cabem.
        var quaseCheia = Preencher(ChunkingExtractor.TamanhoMaximo - 20);
        var markdown = $"""
            # Azul

            ## Preparação

            {quaseCheia}

            ## Fim do jogo

            O jogo acaba quando alguém completa uma linha horizontal.
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Azul > Fim do jogo", chunks[1].Titulo);
    }

    [Fact]
    public void Dividir_SecaoAcimaDoLimite_QuebraEmPedacosDentroDoLimite() {
        var paragrafo = string.Join(" ", Enumerable.Repeat("Uma regra qualquer do manual.", 20));
        var corpo = string.Join("\n\n", Enumerable.Repeat(paragrafo, 10));
        var markdown = $"# Azul\n\n## Pontuação\n\n{corpo}";

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Texto.Length <= ChunkingExtractor.TamanhoMaximo));
        // Todos os pedacos continuam apontando para a secao de origem.
        Assert.All(chunks, c => Assert.Equal("Azul > Pontuação", c.Titulo));
    }

    [Fact]
    public void Dividir_SecaoQuebrada_RepeteACaudaDoPedacoAnterior() {
        var paragrafo = string.Join(" ", Enumerable.Repeat("Uma regra qualquer do manual.", 20));
        var corpo = string.Join("\n\n", Enumerable.Repeat(paragrafo, 10));
        var markdown = $"# Azul\n\n## Pontuação\n\n{corpo}";

        var chunks = ChunkingExtractor.Dividir(markdown);

        // O inicio de cada pedaco reaparece no fim do anterior: e o overlap.
        var inicioDoSegundo = chunks[1].Texto[..50];
        Assert.Contains(inicioDoSegundo, chunks[0].Texto);
    }

    [Fact]
    public void Dividir_ParagrafoUnicoAcimaDoLimite_QuebraEntreFrases() {
        var paragrafo = string.Concat(Enumerable.Repeat("Esta e uma frase completa sobre uma regra do jogo. ", 100));
        var markdown = $"# Azul\n\n## Regras\n\n{paragrafo}";

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Texto.Length <= ChunkingExtractor.TamanhoMaximo));
        Assert.All(chunks, c => Assert.EndsWith(".", c.Texto.TrimEnd()));
    }

    [Fact]
    public void Dividir_TabelaAcimaDoLimite_QuebraPorLinhaERepeteOCabecalho() {
        var linhas = Enumerable.Range(1, 120).Select(i => $"| Peça {i} | Descrição bem longa da peça número {i} do jogo |");
        var tabela = "| Peça | Descrição |\n| --- | --- |\n" + string.Join("\n", linhas);
        var markdown = $"# Azul\n\n## Componentes\n\n{tabela}";

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.Contains("| Peça | Descrição |", c.Texto));
        // Nenhuma linha de tabela pode terminar cortada no meio.
        Assert.All(chunks, c => Assert.All(
            c.Texto.Split('\n').Where(l => l.StartsWith('|')),
            l => Assert.EndsWith("|", l.TrimEnd())));
    }

    [Fact]
    public void Dividir_CercaDeCodigo_NaoTrataHashComoTitulo() {
        var markdown = $"""
            # Azul

            ## Exemplo

            ```
            # isto e um comentario, nao um titulo
            ```

            {Corpo("Continuação da seção.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Single(chunks);
        Assert.Equal("Azul > Exemplo", chunks[0].Titulo);
        Assert.Contains("# isto e um comentario", chunks[0].Texto);
    }

    [Fact]
    public void TextoParaEmbedding_PrefixaOCaminhoDoTitulo() {
        var chunk = new ManualChunk(0, "Azul > Turno", "Escolha uma fábrica.");

        Assert.Equal("Azul > Turno\n\nEscolha uma fábrica.", chunk.TextoParaEmbedding);
    }

    [Fact]
    public void TextoParaEmbedding_SemTitulo_DevolveApenasOTexto() {
        var chunk = new ManualChunk(0, "", "Bem-vindo ao manual.");

        Assert.Equal("Bem-vindo ao manual.", chunk.TextoParaEmbedding);
    }

    [Fact]
    public async Task ExtrairChunksAsync_LeOMarkdownDoArquivo() {
        var caminho = Path.Combine(Path.GetTempPath(), $"manual-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(caminho, $"# Azul\n\n## Preparação\n\n{Corpo("Monte o tabuleiro.")}");

        try {
            var extractor = new ChunkingExtractor(NullLogger<ChunkingExtractor>.Instance);

            var chunks = await extractor.ExtrairChunksAsync(caminho, CancellationToken.None);

            Assert.Single(chunks);
            Assert.Equal("Azul > Preparação", chunks[0].Titulo);
        } finally {
            File.Delete(caminho);
        }
    }
}
