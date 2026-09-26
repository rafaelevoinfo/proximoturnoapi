using Qdrant.Client;
using Qdrant.Client.Grpc;
using ProximoTurnoApi.Application.UseCases.RAG;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Grava os chunks vetorizados no Qdrant. Uma única coleção por ambiente, para todos os jogos:
/// quando não se sabe de que jogo o usuário está falando, a busca corre em tudo e o
/// payload diz qual jogo respondeu; sabendo o jogo, filtra-se por IdJogo.
/// </summary>
public class QdrantManualVectorStore(ILogger<QdrantManualVectorStore> _logger,
                                     QdrantClient _client,
                                     IHostEnvironment _env) : IManualVectorStore {

    public const string COLECAO_MANUAIS = "manuais";
    public const string COLECAO_MANUAIS_DEBUG = "manuais_dev";

    /// <summary>
    /// Desenvolvimento grava numa coleção própria: reindexar apaga e regrava todos os
    /// pontos do link, e fazer isso na coleção de produção estragaria a busca de quem
    /// está usando o site. Mesmo critério das pastas de upload do Cloudinary.
    /// </summary>
    public static string NomeColecao(IHostEnvironment env) =>
        env.IsDevelopment() ? COLECAO_MANUAIS_DEBUG : COLECAO_MANUAIS;

    private string Colecao => NomeColecao(_env);

    public async Task SalvarAsync(int idJogo, int idJogoLink, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken) {
        if (embeddings.Count == 0) {
            return;
        }

        await GarantirColecaoAsync((ulong)embeddings[0].Vetor.Length, cancellationToken);
        // Reindexar um manual que agora rende menos chunks deixaria os antigos para tras,
        // ainda respondendo buscas. Por isso apaga-se o manual inteiro antes de gravar.
        await _client.DeleteAsync(Colecao, MatchInt("IdJogoLink", idJogoLink), cancellationToken: cancellationToken);

        var pontos = embeddings.Select(embedding => Ponto(idJogo, idJogoLink, embedding)).ToList();
        await _client.UpsertAsync(Colecao, pontos, cancellationToken: cancellationToken);

        _logger.LogInformation("{Quantidade} vetores do link {IdJogoLink} do jogo {IdJogo} gravados na coleção {Colecao}.",
                               pontos.Count, idJogoLink, idJogo, Colecao);
    }

    // O padrao do facet e 10: sem um teto alto a reconciliacao enxergaria so uma fatia
    // da colecao e deixaria orfao para tras.
    private const ulong LimiteFacet = 10_000;

    public async Task RemoverAsync(int idJogoLink, CancellationToken cancellationToken) {
        if (!await _client.CollectionExistsAsync(Colecao, cancellationToken)) {
            return;
        }

        var filtro = MatchInt("IdJogoLink", idJogoLink);

        // O delete por filtro do Qdrant devolve status, nao quantidade: sem contar antes, o log
        // anunciaria remocao tambem quando nao havia nada - o que acontece em todo primeiro
        // indice de um link. A consulta a mais roda uma vez por manual, e evita escrita inutil.
        var quantidade = await _client.CountAsync(Colecao, filtro, exact: true, cancellationToken: cancellationToken);
        if (quantidade == 0) {
            _logger.LogDebug("Link {IdJogoLink} não tinha vetores na coleção {Colecao}.", idJogoLink, Colecao);
            return;
        }

        await _client.DeleteAsync(Colecao, filtro, cancellationToken: cancellationToken);

        // A quantidade e informacao nova: vetor sobrando num link que se julgava vazio e sujeira,
        // e hoje isso era indistinguivel de zero.
        _logger.LogInformation("{Quantidade} vetores do link {IdJogoLink} removidos da coleção {Colecao}.",
                               quantidade, idJogoLink, Colecao);
    }

    public async Task<IReadOnlyList<int>> ListarIdsLinksAsync(CancellationToken cancellationToken) {
        if (!await _client.CollectionExistsAsync(Colecao, cancellationToken)) {
            return [];
        }

        var facetas = await _client.FacetAsync(Colecao, "IdJogoLink", limit: LimiteFacet, exact: true, cancellationToken: cancellationToken);

        // O Qdrant devolve faceta com Count 0 para valor que ja nao tem ponto nenhum. Sem este
        // filtro, a reconciliacao le esses ids como "vetores sobrando", enfileira o link com
        // IdJogo 0 e manda remover o que nao existe - a cada start da aplicacao.
        return [.. facetas.Hits.Where(faceta => faceta.Count > 0).Select(faceta => (int)faceta.Value.IntegerValue)];
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
        if (await _client.CollectionExistsAsync(Colecao, cancellationToken)) {
            return;
        }

        await _client.CreateCollectionAsync(
            Colecao,
            new VectorParams { Size = dimensao, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);

        // Sem indice de payload o filtro por jogo degrada conforme a colecao cresce.
        await _client.CreatePayloadIndexAsync(Colecao, "IdJogo", PayloadSchemaType.Integer, cancellationToken: cancellationToken);
        await _client.CreatePayloadIndexAsync(Colecao, "IdJogoLink", PayloadSchemaType.Integer, cancellationToken: cancellationToken);

        _logger.LogInformation("Coleção {Colecao} criada com dimensão {Dimensao} e distância Cosine.", Colecao, dimensao);
    }

    private static Filter MatchInt(string campo, int valor) =>
        new() { Must = { new Condition { Field = new FieldCondition { Key = campo, Match = new Match { Integer = valor } } } } };
}
