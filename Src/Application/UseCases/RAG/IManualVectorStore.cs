namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>Um pedaço de manual devolvido pela busca, com a similaridade à pergunta.</summary>
public sealed record TrechoManual(int IdJogo, int IdJogoLink, string Titulo, string Texto, float Score);

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

    /// <summary>
    /// Trechos do manual mais parecidos com a pergunta, sempre de um jogo só. O jogo é
    /// obrigatório de propósito: uma busca sem filtro misturaria regras de jogos diferentes
    /// na mesma resposta, e isso não pode depender da disciplina de quem chama.
    /// </summary>
    Task<IReadOnlyList<TrechoManual>> BuscarAsync(int idJogo, ReadOnlyMemory<float> vetor, int quantidade, CancellationToken cancellationToken);
}
