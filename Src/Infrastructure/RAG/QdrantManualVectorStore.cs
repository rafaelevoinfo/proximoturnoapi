using Qdrant.Client;
using Qdrant.Client.Grpc;
using ProximoTurnoApi.Application.UseCases.RAG;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Grava os chunks vetorizados no Qdrant. Uma única coleção para todos os jogos:
/// quando não se sabe de que jogo o usuário está falando, a busca corre em tudo e o
/// payload diz qual jogo respondeu; sabendo o jogo, filtra-se por IdJogo.
/// </summary>
public class QdrantManualVectorStore(ILogger<QdrantManualVectorStore> _logger,
                                     QdrantClient _client) : IManualVectorStore {

    public const string COLECAO_MANUAIS = "manuais";

    // O SDK .NET fala gRPC, que no Qdrant Cloud atende na 6334 (a 6333 e REST).
    private const int PortaGrpc = 6334;

    public async Task SalvarAsync(int idJogo, int idJogoLink, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken) {
        if (embeddings.Count == 0) {
            return;
        }

        await GarantirColecaoAsync((ulong)embeddings[0].Vetor.Length, cancellationToken);
        // Reindexar um manual que agora rende menos chunks deixaria os antigos para tras,
        // ainda respondendo buscas. Por isso apaga-se o manual inteiro antes de gravar.
        await _client.DeleteAsync(COLECAO_MANUAIS, MatchInt("IdJogoLink", idJogoLink), cancellationToken: cancellationToken);

        var pontos = embeddings.Select(embedding => Ponto(idJogo, idJogoLink, embedding)).ToList();
        await _client.UpsertAsync(COLECAO_MANUAIS, pontos, cancellationToken: cancellationToken);

        _logger.LogInformation("{Quantidade} vetores do link {IdJogoLink} do jogo {IdJogo} gravados na coleção {Colecao}.",
                               pontos.Count, idJogoLink, idJogo, COLECAO_MANUAIS);
    }

    /// <summary>
    /// Monta o ponto do Qdrant a partir do chunk. O texto vai no payload porque a busca
    /// precisa devolver a resposta pronta: o conteúdo não existe em nenhum outro lugar.
    /// </summary>
    public static PointStruct Ponto(int idJogo, int idJogoLink, ChunkEmbedding embedding) {
        return new PointStruct {
            // O manual e apagado por filtro antes do upsert, entao o id nao precisa
            // ser deterministico - so precisa nao colidir.
            Id = Guid.NewGuid(),
            Vectors = embedding.Vetor.ToArray(),
            Payload = {
                ["IdJogo"] = idJogo,
                ["IdJogoLink"] = idJogoLink,
                ["Ordem"] = embedding.Chunk.Ordem,
                ["Titulo"] = embedding.Chunk.Titulo,
                ["Texto"] = embedding.Chunk.Texto,
            }
        };
    }

    /// <summary>
    /// Cria a coleção na primeira gravação. O tamanho vem do vetor recebido, não de uma
    /// constante: trocar o modelo de embedding muda a dimensão e o erro tem que aparecer.
    /// </summary>
    private async Task GarantirColecaoAsync(ulong dimensao, CancellationToken cancellationToken) {
        if (await _client.CollectionExistsAsync(COLECAO_MANUAIS, cancellationToken)) {
            return;
        }

        await _client.CreateCollectionAsync(
            COLECAO_MANUAIS,
            new VectorParams { Size = dimensao, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);

        // Sem indice de payload o filtro por jogo degrada conforme a colecao cresce.
        await _client.CreatePayloadIndexAsync(COLECAO_MANUAIS, "IdJogo", PayloadSchemaType.Integer, cancellationToken: cancellationToken);
        await _client.CreatePayloadIndexAsync(COLECAO_MANUAIS, "IdJogoLink", PayloadSchemaType.Integer, cancellationToken: cancellationToken);

        _logger.LogInformation("Coleção {Colecao} criada com dimensão {Dimensao} e distância Cosine.", COLECAO_MANUAIS, dimensao);
    }

    private static Filter MatchInt(string campo, int valor) =>
        new() { Must = { new Condition { Field = new FieldCondition { Key = campo, Match = new Match { Integer = valor } } } } };
}
