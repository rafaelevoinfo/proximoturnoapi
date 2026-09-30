using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class SituacaoManuaisTests {

    private const string Url = "https://site/uploads/manual.pdf";
    private readonly FakeIndexacaoManualRepository _repo = new();

    private SituacaoManuais Caso() => new(_repo);

    [Fact]
    public async Task Listagem_ResumeCadaJogo() {
        _repo.Adicionar(1, idJogo: 10, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Indexado });
        _repo.Adicionar(2, idJogo: 10, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Duplicado });
        _repo.Adicionar(3, idJogo: 20, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Falhou, Tentativas = 3 });
        _repo.Adicionar(4, idJogo: 30);
        _repo.Adicionar(5, idJogo: 40, tipo: TipoLink.Video);
        List<JogoCardDTO> jogos = [new() { Id = 10 }, new() { Id = 20 }, new() { Id = 30 }, new() { Id = 40 }];

        await Caso().PreencherAsync(jogos);

        Assert.Equal([SituacaoManual.Indexado, SituacaoManual.Falhou, SituacaoManual.Pendente, SituacaoManual.SemManual],
                     jogos.Select(j => j.Manual!.Value));
    }

    [Fact]
    public async Task Edicao_PreencheSoOsLinksDeRegra() {
        _repo.Adicionar(1, idJogo: 10, indexacao: new JogoLinkIndexacao {
            Url = Url, Status = StatusIndexacao.Indexado, QuantidadeChunks = 42, DataIndexacao = new DateTime(2026, 9, 30),
        });
        _repo.Adicionar(2, idJogo: 10, tipo: TipoLink.Video);
        var jogo = new JogoDTO {
            Id = 10,
            Links = [new() { Id = 1, Tipo = TipoLink.Regra, Url = Url }, new() { Id = 2, Tipo = TipoLink.Video, Url = "v" }],
        };

        await Caso().PreencherAsync(jogo);

        var regra = jogo.Links![0].Indexacao!;
        Assert.Equal(SituacaoManual.Indexado, regra.Situacao);
        Assert.Equal(42, regra.QuantidadeTrechos);
        Assert.Null(jogo.Links[1].Indexacao);
    }

    // PDF trocado no link: a linha ainda e da URL antiga, entao o manual novo esta na fila.
    [Fact]
    public void UrlTrocada_EhPendente() {
        var linha = new JogoLinkIndexacao { Url = "https://site/uploads/antigo.pdf", Status = StatusIndexacao.Indexado };

        Assert.Equal(SituacaoManual.Pendente, IndexacaoLinkDTO.De(Url, linha).Situacao);
    }

    [Fact]
    public void Falha_TrazOErroEAsTentativas() {
        var linha = new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Falhou, Tentativas = 3, UltimoErro = "PDF corrompido" };

        var dto = IndexacaoLinkDTO.De(Url, linha);

        Assert.Equal(SituacaoManual.Falhou, dto.Situacao);
        Assert.True(dto.TentativasEsgotadas);
        Assert.Equal("PDF corrompido", dto.UltimoErro);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public void Consulta_CompilaNoProviderMySql(int? idJogo) {
        var opcoes = new DbContextOptionsBuilder<DatabaseContext>()
            .UseMySQL("server=localhost;database=teste;user=teste;password=teste")
            .Options;
        using var db = new DatabaseContext(opcoes);

        var sql = IndexacaoManualRepository.ConsultaIndexacoes(db, idJogo).ToQueryString();

        Assert.Contains("JOGO_LINK_INDEXACAO", sql);
    }
}
