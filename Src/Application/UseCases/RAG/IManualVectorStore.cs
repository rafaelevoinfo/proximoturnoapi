namespace ProximoTurnoApi.Application.UseCases.RAG;

public interface IManualVectorStore {

    /// <summary>
    /// Grava os vetores do manual, substituindo o que já havia para este link.
    /// Os ids do jogo vêm por parâmetro porque o <see cref="ChunkEmbedding"/> não os
    /// carrega: chunking e embedding não precisam saber de que jogo o texto veio.
    /// </summary>
    Task SalvarAsync(int idJogo, int idJogoLink, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken);
}
