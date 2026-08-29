namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Um chunk com o vetor que o representa. O chunk viaja junto porque quem
/// grava precisa do texto e do título ao lado do vetor.
/// </summary>
public sealed record ChunkEmbedding(ManualChunk Chunk, ReadOnlyMemory<float> Vetor);

public interface IEmbeddingExtractor {
    Task<IReadOnlyList<ChunkEmbedding>> GerarEmbeddingsAsync(IReadOnlyList<ManualChunk> chunks, CancellationToken cancellationToken);
}
