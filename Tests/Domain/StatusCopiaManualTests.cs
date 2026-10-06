using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
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

    // ---- cópias editadas no formulário do jogo ----

    private static List<StatusJogo> StatusDe(IEnumerable<JogoCopia> copias) => copias.Select(c => c.Status).ToList();

    private static StatusDoJogo.CopiaDesejada D(int id, StatusJogo status) => new(id, status);

    [Fact]
    public void Formulario_CriaNovasTrocaStatusEDesativaAsRemovidas() {
        var copias = Copias(StatusJogo.Disponivel, StatusJogo.Disponivel, StatusJogo.Manutencao);

        var erros = StatusDoJogo.SincronizarCopias(copias, [
            D(1, StatusJogo.ApenasEmEventos),   // troca
            D(3, StatusJogo.Manutencao),        // mantida igual
            D(0, StatusJogo.Disponivel),        // nova
            D(0, StatusJogo.Manutencao),        // nova já em manutenção
        ]);                                     // a #2 saiu do formulário

        Assert.Empty(erros);
        Assert.Equal([StatusJogo.ApenasEmEventos, StatusJogo.Desativado, StatusJogo.Manutencao, StatusJogo.Disponivel, StatusJogo.Manutencao],
                     StatusDe(copias));
        Assert.Equal([0, 0], copias.Skip(3).Select(c => c.Id));
    }

    [Fact]
    public void Formulario_CopiaDesativadaQueNaoVemNaListaFicaComoEsta() {
        var copias = Copias(StatusJogo.Disponivel, StatusJogo.Desativado);

        Assert.Empty(StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Disponivel)]));
        Assert.Equal([StatusJogo.Disponivel, StatusJogo.Desativado], StatusDe(copias));
    }

    [Theory]
    [InlineData(StatusJogo.Reservado, "reservada")]
    [InlineData(StatusJogo.Alugado, "alugada")]
    public void Formulario_CopiaDoFluxoDePedido_NaoMudaNemSaiENadaEhAplicado(StatusJogo doPedido, string situacao) {
        var copias = Copias(doPedido, StatusJogo.Disponivel);

        var trocar = StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Manutencao), D(2, StatusJogo.ApenasEmEventos)]);
        var excluir = StatusDoJogo.SincronizarCopias(copias, [D(2, StatusJogo.ApenasEmEventos), D(0, StatusJogo.Disponivel)]);

        Assert.Contains(trocar, e => e.Contains("#1") && e.Contains(situacao));
        Assert.Contains(excluir, e => e.Contains("#1") && e.Contains("não pode ser excluída"));
        // Com erro nada muda, nem a cópia #2 que estava certa, nem a nova.
        Assert.Equal([doPedido, StatusJogo.Disponivel], StatusDe(copias));
    }

    [Fact]
    public void Formulario_CopiaDoFluxoDePedidoMantidaComOMesmoStatus_EhAceita() {
        var copias = Copias(StatusJogo.Alugado, StatusJogo.Disponivel);

        Assert.Empty(StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Alugado), D(2, StatusJogo.Manutencao)]));
        Assert.Equal([StatusJogo.Alugado, StatusJogo.Manutencao], StatusDe(copias));
    }

    [Theory]
    [InlineData(StatusJogo.Reservado)]
    [InlineData(StatusJogo.Alugado)]
    [InlineData(StatusJogo.Desativado)]
    public void Formulario_NaoAtribuiStatusDoFluxoDePedidoNemDesativado(StatusJogo status) {
        var copias = Copias(StatusJogo.Disponivel);

        Assert.NotEmpty(StatusDoJogo.SincronizarCopias(copias, [D(1, status)]));
        Assert.NotEmpty(StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Disponivel), D(0, status)]));
        Assert.Single(copias);
        Assert.Equal(StatusJogo.Disponivel, copias[0].Status);
    }

    [Fact]
    public void Formulario_SemNenhumaCopia_Recusa() {
        var copias = Copias(StatusJogo.Disponivel);

        var erros = StatusDoJogo.SincronizarCopias(copias, []);

        Assert.Contains("pelo menos uma cópia", Assert.Single(erros));
        Assert.Equal(StatusJogo.Disponivel, copias[0].Status);
    }

    [Fact]
    public void Formulario_CopiaDeOutroJogo_Recusa() {
        var copias = Copias(StatusJogo.Disponivel);

        Assert.Contains("#99", Assert.Single(StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Disponivel), D(99, StatusJogo.Disponivel)])));
    }

    [Fact]
    public void Formulario_DesativadaReapareceNaLista_VoltaAoStatusEscolhido() {
        var copias = Copias(StatusJogo.Desativado);

        Assert.Empty(StatusDoJogo.SincronizarCopias(copias, [D(1, StatusJogo.Disponivel)]));
        Assert.Equal(StatusJogo.Disponivel, copias[0].Status);
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
