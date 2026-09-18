namespace ProximoTurnoApi.Application.UseCases.RAG;

public interface IManualVectorStore {

    /// <summary>
    /// Grava os vetores do manual, substituindo o que já havia para este link.
    /// Os ids do jogo vêm por parâmetro porque o <see cref="ChunkEmbedding"/> não os
    /// carrega: chunking e embedding não precisam saber de que jogo o texto veio.
    /// </summary>
    Task SalvarAsync(int idJogo, int idJogoLink, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken);

    /// <summary>
    /// Apaga os vetores de um link. Chamado antes de reindexar e sempre que o link deixa
    /// de poder responder buscas: apagado, virado vídeo ou de um jogo desativado.
    /// </summary>
    Task RemoverAsync(int idJogoLink, CancellationToken cancellationToken);

    /// <summary>
    /// Links que têm vetores gravados. É o lado do Qdrant da reconciliação: o que está
    /// aqui e não deveria estar vira job de remoção.
    /// </summary>
    Task<IReadOnlyList<int>> ListarIdsLinksAsync(CancellationToken cancellationToken);
}
