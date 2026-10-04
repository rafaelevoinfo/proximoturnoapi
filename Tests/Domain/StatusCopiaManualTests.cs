using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class StatusCopiaManualTests {

    private static List<JogoCopia> Copias(params StatusJogo[] status) =>
        status.Select((s, i) => new JogoCopia { Id = i + 1, IdJogo = 10, Status = s }).ToList();

    // ---- status do jogo a partir das cópias ----

    [Theory]
    [InlineData(new[] { StatusJogo.Manutencao, StatusJogo.Disponivel }, StatusJogo.Disponivel)]
    [InlineData(new[] { StatusJogo.ApenasEmEventos, StatusJogo.Alugado }, StatusJogo.Alugado)]
    [InlineData(new[] { StatusJogo.Manutencao, StatusJogo.Reservado }, StatusJogo.Reservado)]
    [InlineData(new[] { StatusJogo.ApenasEmEventos, StatusJogo.ApenasEmEventos }, StatusJogo.ApenasEmEventos)]
    [InlineData(new[] { StatusJogo.Manutencao, StatusJogo.ApenasEmEventos }, StatusJogo.ApenasEmEventos)]
    [InlineData(new[] { StatusJogo.Manutencao, StatusJogo.Desativado }, StatusJogo.Manutencao)]
    [InlineData(new[] { StatusJogo.Desativado, StatusJogo.Desativado }, StatusJogo.Desativado)]
    public void StatusDoJogo_VemDaMelhorCopia(StatusJogo[] copias, StatusJogo esperado) =>
        Assert.Equal(esperado, StatusDoJogo.Calcular(Copias(copias)));

    [Fact]
    public void StatusDoJogo_SemCopias_DeixaOPadraoDeQuemChama() {
        Assert.Null(StatusDoJogo.Calcular(null));
        Assert.Null(StatusDoJogo.Calcular([]));
    }

    [Fact]
    public void Card_MostraOStatusManual() {
        var jogo = new Jogo { Id = 10, Nome = "Catan", IdCategoria = 1, Copias = Copias(StatusJogo.Manutencao) };

        Assert.Equal(StatusJogo.Manutencao, JogoCardDTO.FromModel(jogo).Status);
        Assert.Equal(StatusJogo.Manutencao, JogoPublicDTO.FromModel(jogo).Status);
    }

    [Fact]
    public void ValoresGravadosNoBancoNaoMudaram() {
        Assert.Equal(0, (short)StatusJogo.Disponivel);
        Assert.Equal(1, (short)StatusJogo.Reservado);
        Assert.Equal(2, (short)StatusJogo.Alugado);
        Assert.Equal(4, (short)StatusJogo.Desativado);
        Assert.False(Enum.IsDefined(typeof(StatusJogo), (short)3));
    }

    // ---- pedido ----

    [Theory]
    [InlineData(StatusJogo.ApenasEmEventos)]
    [InlineData(StatusJogo.Manutencao)]
    public void Pedido_NaoAceitaCopiaComStatusManual(StatusJogo status) {
        var pedido = new Pedido(new Cliente { Id = 1, Nome = "C", Email = "c@c.com", Telefone = "1", Endereco = "R" }, "dinheiro", "retirada");
        var copia = new JogoCopia { Id = 1, Status = status, Jogo = new Jogo { Id = 10, Nome = "Catan", IdCategoria = 1 } };

        Assert.False(pedido.AdicionarItem(new ItemPedido { Id = 1, JogoCopia = copia, IdPeriodo = 1, Valor = 50m }));
        Assert.Equal(status, copia.Status);
    }

    // ---- troca manual ----

    private static (AlterarStatusCopia UseCase, FakeJogoRepository Repo) Montar(StatusJogo statusAtual) {
        var repo = new FakeJogoRepository { Copias = Copias(statusAtual) };
        return (new AlterarStatusCopia(repo), repo);
    }

    [Theory]
    [InlineData(StatusJogo.Disponivel, StatusJogo.ApenasEmEventos)]
    [InlineData(StatusJogo.Disponivel, StatusJogo.Manutencao)]
    [InlineData(StatusJogo.Manutencao, StatusJogo.Disponivel)]
    [InlineData(StatusJogo.ApenasEmEventos, StatusJogo.Manutencao)]
    public async Task TrocaEntreStatusManuais(StatusJogo atual, StatusJogo novo) {
        var (useCase, repo) = Montar(atual);

        Assert.True(await useCase.ExecuteAsync(10, 1, novo));
        Assert.Equal(novo, repo.Copias[0].Status);
        Assert.Single(repo.CopiasSalvas);
    }

    [Theory]
    [InlineData(StatusJogo.Reservado, "reservada")]
    [InlineData(StatusJogo.Alugado, "alugada")]
    [InlineData(StatusJogo.Desativado, "desativada")]
    public async Task CopiaNoFluxoDePedidoOuDesativada_NaoMuda(StatusJogo atual, string motivo) {
        var (useCase, repo) = Montar(atual);

        Assert.False(await useCase.ExecuteAsync(10, 1, StatusJogo.Manutencao));
        Assert.Equal(atual, repo.Copias[0].Status);
        Assert.Empty(repo.CopiasSalvas);
        Assert.Contains(motivo, useCase.AggregateErrors());
        Assert.Equal(UseCaseNotificationType.BadRequest, useCase.Notifications.First().Type);
    }

    [Theory]
    [InlineData(StatusJogo.Reservado)]
    [InlineData(StatusJogo.Alugado)]
    [InlineData(StatusJogo.Desativado)]
    public async Task NaoAtribuiStatusDoFluxoDePedido(StatusJogo novo) {
        var (useCase, repo) = Montar(StatusJogo.Disponivel);

        Assert.False(await useCase.ExecuteAsync(10, 1, novo));
        Assert.Equal(StatusJogo.Disponivel, repo.Copias[0].Status);
    }

    [Theory]
    [InlineData(10, 99)]
    [InlineData(11, 1)]
    public async Task CopiaInexistenteOuDeOutroJogo_NotFound(int idJogo, int idCopia) {
        var (useCase, _) = Montar(StatusJogo.Disponivel);

        Assert.False(await useCase.ExecuteAsync(idJogo, idCopia, StatusJogo.Manutencao));
        Assert.Equal(UseCaseNotificationType.NotFound, useCase.Notifications.First().Type);
    }

    // ---- filtro da listagem do admin ----

    [Fact]
    public void Filtro_Indisponiveis_SemDisponivelEComAlugadaReservadaOuManutencao() {
        var condicoes = JogoRepository.CondicoesSituacao(SituacaoJogoFiltro.Indisponiveis).ToList();

        Assert.Equal(2, condicoes.Count);
        Assert.StartsWith("NOT EXISTS", condicoes[0]);
        Assert.Contains("jc.STATUS IN (0)", condicoes[0]);
        Assert.Contains("jc.STATUS IN (1, 2, 6)", condicoes[1]);
    }

    [Fact]
    public void Filtro_ApenasEmEventos_TodasAsAtivasEmEvento() {
        var condicoes = JogoRepository.CondicoesSituacao(SituacaoJogoFiltro.ApenasEmEventos).ToList();

        Assert.Contains("jc.STATUS IN (5)", condicoes[0]);
        Assert.Equal("NOT EXISTS (SELECT 1 FROM JOGO_COPIA jc WHERE jc.ID_JOGO = j.ID AND jc.STATUS != 4 AND jc.STATUS != 5)", condicoes[1]);
    }

    [Fact]
    public void Filtro_SemSituacao_SoEscondeDesativados() =>
        Assert.Equal(["EXISTS (SELECT 1 FROM JOGO_COPIA jc WHERE jc.ID_JOGO = j.ID AND jc.STATUS != 4)"],
                     JogoRepository.CondicoesSituacao(null));
}
