using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.RAG;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Transforma os chunks do manual em vetores. O cliente vem por injeção para que a
/// política de lote e o pareamento vetor-chunk possam ser testados sem chamar a API.
/// </summary>
public class EmbeddingExtractor(ILogger<EmbeddingExtractor> _logger,
                                IEmbeddingGenerator<string, Embedding<float>> _gerador) : IEmbeddingExtractor {

    // O endpoint aceita um array por requisicao, e um manual tipico tem ~10 chunks:
    // cabe numa chamada so. O teto existe para um manual fora da curva nao virar
    // um corpo unico gigante, nao para economizar chamadas.
    public const int TamanhoLote = 32;

    public async Task<IReadOnlyList<ChunkEmbedding>> GerarEmbeddingsAsync(IReadOnlyList<ManualChunk> chunks, CancellationToken cancellationToken) {
        if (chunks.Count == 0) {
            return [];
        }

        var resultado = new List<ChunkEmbedding>(chunks.Count);
        var lotes = 0;

        for (var inicio = 0; inicio < chunks.Count; inicio += TamanhoLote) {
            var lote = chunks.Skip(inicio).Take(TamanhoLote).ToList();
            var entradas = lote.Select(chunk => chunk.TextoParaEmbedding).ToList();

            var vetores = await _gerador.GenerateAsync(entradas, cancellationToken: cancellationToken);
            lotes++;

            // Vetor pareado ao chunk errado nao levanta erro nenhum: a busca so passa a
            // responder a regra vizinha. Melhor derrubar a indexacao deste manual.
            if (vetores.Count != lote.Count) {
                throw new InvalidOperationException(
                    $"O provedor devolveu {vetores.Count} embeddings para {lote.Count} chunks.");
            }

            resultado.AddRange(lote.Select((chunk, indice) => new ChunkEmbedding(chunk, vetores[indice].Vector)));
        }

        _logger.LogInformation("{Quantidade} embeddings gerados em {Lotes} lote(s), dimensão {Dimensao}.",
                               resultado.Count, lotes, resultado[0].Vetor.Length);
        return resultado;
    }
}
