using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ResolvedorJogoChatTests {

    private static readonly List<JogoChat> Catalogo = [
        new(1, "Catan", true),
        new(2, "Catan: Cidades e Cavaleiros", true),
        new(3, "Ticket to Ride", true),
        new(4, "War", true),
        new(5, "Warhammer Underworlds", true),
        new(6, "Código Secreto", true),
        new(7, "Wingspan", true),
    ];

    private static List<int> Ids(string mencionado) =>
        [.. ResolvedorJogoChat.Candidatos(mencionado, Catalogo).Select(j => j.Id)];

    [Fact]
    public void NomeExato_VemPrimeiro() {
        Assert.Equal([1, 2], Ids("Catan"));
    }

    [Fact]
    public void IgnoraAcentoECaixa() {
        Assert.Equal(6, Ids("codigo secreto")[0]);
    }

    [Fact]
    public void ToleraErroDeDigitacao() {
        Assert.Equal(3, Ids("tiket to ride")[0]);
    }

    [Fact]
    public void Subtitulo_AchaOJogoCompleto() {
        Assert.Equal(2, Ids("cidades e cavaleiros")[0]);
    }

    [Fact]
    public void PalavraCurta_NaoCasaDentroDeOutraPalavra() {
        Assert.Equal([4], Ids("war"));
    }

    [Fact]
    public void NomeSemRelacao_NaoTrazOpcao() {
        Assert.Empty(Ids("Monopoly"));
    }

    [Fact]
    public void NoMaximoTresOpcoes() {
        var muitos = Enumerable.Range(1, 10).Select(i => new JogoChat(i, $"Catan edição {i}", true)).ToList();

        Assert.Equal(3, ResolvedorJogoChat.Candidatos("catan", muitos).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MencaoVazia_NaoTrazOpcao(string? mencionado) {
        Assert.Empty(ResolvedorJogoChat.Candidatos(mencionado, Catalogo));
    }

    [Fact]
    public void CitadosNaMensagem_AchaONomeNoMeioDaFrase() {
        var ids = ResolvedorJogoChat.CitadosNaMensagem("como pontua no Código Secreto?", Catalogo).Select(j => j.Id);

        Assert.Equal([6], ids);
    }

    [Fact]
    public void CitadosNaMensagem_NomeBaseTrazAsVariacoes() {
        var ids = ResolvedorJogoChat.CitadosNaMensagem("no catan posso trocar?", Catalogo).Select(j => j.Id);

        Assert.Equal([1, 2], ids);
    }

    [Fact]
    public void CitadosNaMensagem_SemJogo_Vazio() {
        Assert.Empty(ResolvedorJogoChat.CitadosNaMensagem("como faço para ganhar pontos?", Catalogo));
    }

    [Fact]
    public void CitadosNaMensagem_PalavraDentroDeOutra_NaoConta() {
        Assert.Empty(ResolvedorJogoChat.CitadosNaMensagem("software de guerra", Catalogo));
    }

    [Theory]
    [InlineData("Reload", true)]
    [InlineData("ticket to ride", true)]
    [InlineData("Oi", false)]
    [InlineData("bom dia", false)]
    [InlineData("Qual o objetivo?", false)]
    [InlineData("como faço para pontuar no fim da partida", false)]
    [InlineData("", false)]
    public void PareceSoONome(string mensagem, bool esperado) {
        Assert.Equal(esperado, ResolvedorJogoChat.PareceSoONome(mensagem));
    }

    [Fact]
    public void Identificar_PerguntaLongaSemNome_NaoTentaAproximar() {
        Assert.Empty(ResolvedorJogoChat.Identificar("como faço para ganhar no final da partida", Catalogo));
    }
}
