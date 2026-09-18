using Microsoft.Extensions.Hosting;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.RAG;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class QdrantManualVectorStoreTests {

    private static ChunkEmbedding Embedding(int ordem = 3, string titulo = "Azul > Turno", string texto = "Escolha uma fábrica.") =>
        new(new ManualChunk(ordem, titulo, texto), new float[] { 0.1f, 0.2f, 0.3f });

    [Fact]
    public void Ponto_GravaOsIdsNoPayload() {
        var ponto = QdrantManualVectorStore.Ponto(idJogo: 42, idJogoLink: 17, Embedding());

        // IdJogo permite filtrar por jogo quando se sabe qual e; IdJogoLink e o que
        // deixa uma reindexacao apagar so os pontos deste manual.
        Assert.Equal(42L, ponto.Payload["IdJogo"].IntegerValue);
        Assert.Equal(17L, ponto.Payload["IdJogoLink"].IntegerValue);
    }

    [Fact]
    public void Ponto_GravaOTextoEOTituloDoChunk() {
        var ponto = QdrantManualVectorStore.Ponto(1, 1, Embedding(ordem: 3, titulo: "Azul > Turno", texto: "Escolha uma fábrica."));

        // Sem o texto no payload a busca devolveria so ids e nao daria para montar
        // a resposta: o conteudo do chunk nao existe em nenhum outro lugar.
        Assert.Equal(3L, ponto.Payload["Ordem"].IntegerValue);
        Assert.Equal("Azul > Turno", ponto.Payload["Titulo"].StringValue);
        Assert.Equal("Escolha uma fábrica.", ponto.Payload["Texto"].StringValue);
    }

    [Fact]
    public void Ponto_GravaOVetorDoEmbedding() {
        var ponto = QdrantManualVectorStore.Ponto(1, 1, Embedding());

        // Em 1.19 o vetor denso mora em Vector.Dense; Vector.Data e o campo legado.
        Assert.Equal([0.1f, 0.2f, 0.3f], ponto.Vectors.Vector.Dense.Data);
    }

    [Fact]
    public void Ponto_GeraIdsDiferentesParaCadaChunk() {
        // Os pontos do manual sao apagados por filtro antes do upsert, entao o id nao
        // precisa ser deterministico - mas nao pode colidir entre chunks.
        var primeiro = QdrantManualVectorStore.Ponto(1, 1, Embedding(ordem: 0));
        var segundo = QdrantManualVectorStore.Ponto(1, 1, Embedding(ordem: 1));

        Assert.NotEqual(primeiro.Id, segundo.Id);
    }

    [Fact]
    public void NomeColecao_UsaColecaoSeparadaEmDesenvolvimento() {
        // Reindexar em dev apaga e regrava os pontos do link; na colecao de producao
        // isso derrubaria a busca de quem esta usando o site.
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        Assert.Equal(QdrantManualVectorStore.COLECAO_MANUAIS_DEBUG, QdrantManualVectorStore.NomeColecao(env));

        // Separar so vale se os nomes forem mesmo diferentes.
        Assert.NotEqual(QdrantManualVectorStore.COLECAO_MANUAIS, QdrantManualVectorStore.COLECAO_MANUAIS_DEBUG);
    }

    [Fact]
    public void NomeColecao_UsaColecaoDeProducaoForaDeDesenvolvimento() {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };

        Assert.Equal(QdrantManualVectorStore.COLECAO_MANUAIS, QdrantManualVectorStore.NomeColecao(env));
    }
}
