using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EmbeddingExtractorTests {

    /// <summary>
    /// Devolve um vetor de um elemento com um contador global, para que o teste consiga
    /// dizer qual chunk recebeu qual vetor mesmo depois de a lista passar por vários lotes.
    /// </summary>
    private sealed class GeradorFalso : IEmbeddingGenerator<string, Embedding<float>> {
        private int _proximo;

        public List<string[]> Lotes { get; } = [];
        public int? TruncarEm { get; set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) {

            var entradas = values.ToArray();
            Lotes.Add(entradas);

            var vetores = entradas.Select(_ => new Embedding<float>(new float[] { _proximo++ })).ToList();
            if (TruncarEm is int limite) {
                vetores = [.. vetores.Take(limite)];
            }

            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(vetores));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static EmbeddingExtractor Extractor(GeradorFalso gerador) =>
        new(NullLogger<EmbeddingExtractor>.Instance, gerador);

    private static List<ManualChunk> Chunks(int quantidade) =>
        [.. Enumerable.Range(0, quantidade).Select(i => new ManualChunk(i, $"Azul > Regra {i}", $"Texto da regra {i}."))];

    [Fact]
    public async Task GerarEmbeddingsAsync_EnviaOTextoComOCaminhoDoTitulo() {
        var gerador = new GeradorFalso();
        var chunks = Chunks(1);

        await Extractor(gerador).GerarEmbeddingsAsync(chunks, CancellationToken.None);

        // O que vai para o modelo e o TextoParaEmbedding, nao o Texto cru: sem o titulo
        // o vetor perde a informacao de que regra o trecho descreve.
        Assert.Equal("Azul > Regra 0\n\nTexto da regra 0.", Assert.Single(gerador.Lotes)[0]);
    }

    [Fact]
    public async Task GerarEmbeddingsAsync_ParaCadaChunkDevolveOVetorCorrespondente() {
        var gerador = new GeradorFalso();
        var chunks = Chunks(3);

        var resultado = await Extractor(gerador).GerarEmbeddingsAsync(chunks, CancellationToken.None);

        Assert.Equal(3, resultado.Count);
        Assert.Equal(chunks, resultado.Select(r => r.Chunk));
        Assert.Equal([0f, 1f, 2f], resultado.Select(r => r.Vetor.Span[0]));
    }

    [Fact]
    public async Task GerarEmbeddingsAsync_MaisChunksQueOLote_DivideEmVariasChamadas() {
        var gerador = new GeradorFalso();
        var total = EmbeddingExtractor.TamanhoLote * 2 + 5;

        var resultado = await Extractor(gerador).GerarEmbeddingsAsync(Chunks(total), CancellationToken.None);

        Assert.Equal(3, gerador.Lotes.Count);
        Assert.All(gerador.Lotes, lote => Assert.True(lote.Length <= EmbeddingExtractor.TamanhoLote));
        Assert.Equal(total, resultado.Count);
    }

    [Fact]
    public async Task GerarEmbeddingsAsync_VariosLotes_PreservaAOrdemDosChunks() {
        var gerador = new GeradorFalso();
        var total = EmbeddingExtractor.TamanhoLote * 2 + 5;

        var resultado = await Extractor(gerador).GerarEmbeddingsAsync(Chunks(total), CancellationToken.None);

        // Um vetor pareado ao chunk errado e um erro silencioso: a busca passa a
        // responder a regra vizinha. Por isso a ordem e verificada ponta a ponta.
        Assert.Equal(Enumerable.Range(0, total), resultado.Select(r => r.Chunk.Ordem));
        Assert.Equal(Enumerable.Range(0, total).Select(i => (float)i), resultado.Select(r => r.Vetor.Span[0]));
    }

    [Fact]
    public async Task GerarEmbeddingsAsync_SemChunks_NaoChamaOProvedor() {
        var gerador = new GeradorFalso();

        var resultado = await Extractor(gerador).GerarEmbeddingsAsync([], CancellationToken.None);

        Assert.Empty(resultado);
        Assert.Empty(gerador.Lotes);
    }

    [Fact]
    public async Task GerarEmbeddingsAsync_ProvedorDevolveMenosVetores_Lanca() {
        var gerador = new GeradorFalso { TruncarEm = 1 };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Extractor(gerador).GerarEmbeddingsAsync(Chunks(2), CancellationToken.None));

        Assert.Contains("2", erro.Message);
    }
}
