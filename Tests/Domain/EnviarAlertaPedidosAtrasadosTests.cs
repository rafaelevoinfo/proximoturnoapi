using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EnviarAlertaPedidosAtrasadosTests {
    private static readonly DateTime Hoje = new(2026, 11, 20);
    private static readonly DateTime AsSeis = Hoje.AddHours(6);
    private int _proximoIdItem = 1;

    private static (EnviarAlertaPedidosAtrasados, FakePedidoRepository, FakeNotificationService) Criar() {
        var repo = new FakePedidoRepository();
        var notificacao = new FakeNotificationService();
        return (new EnviarAlertaPedidosAtrasados(repo, notificacao, NullLogger<EnviarAlertaPedidosAtrasados>.Instance), repo, notificacao);
    }

    /// <summary>Pedido entregue com um item por data de devolução informada.</summary>
    private Pedido PedidoEntregue(Cliente cliente, int idPedido, params DateTime[] devolucoes) {
        var pedido = new Pedido(cliente) { Id = idPedido };
        foreach (var devolucao in devolucoes) {
            var id = _proximoIdItem++;
            var copia = PedidoTestFactory.Copia(idCopia: id, idJogo: id, idCategoria: 1);
            copia.Jogo!.Nome = $"Jogo {id}";
            pedido.AdicionarItem(new ItemPedido { Id = id, JogoCopia = copia, IdPeriodo = 1, Valor = 10m });
            var item = pedido.Items[^1];
            item.Status = StatusPedido.Entregue;
            item.DataDevolucao = devolucao.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        }
        return pedido;
    }

    [Fact]
    public async Task UmResumoComOsPedidosAtrasados_ComOMaiorAtrasoDeCada() {
        var (useCase, repo, notificacao) = Criar();
        var ana = PedidoTestFactory.Cliente(id: 1, email: "ana@teste.com");
        var bia = PedidoTestFactory.Cliente(id: 2, email: "bia@teste.com");
        repo.Pedidos.Add(PedidoEntregue(ana, 10, Hoje.AddDays(-1), Hoje.AddDays(-3), Hoje));
        repo.Pedidos.Add(PedidoEntregue(bia, 11, Hoje.AddDays(-2)));
        repo.Pedidos.Add(PedidoEntregue(bia, 12, Hoje, Hoje.AddDays(2)));

        var quantidade = await useCase.ExecuteAsync(AsSeis);

        Assert.Equal(2, quantidade);
        var resumo = Assert.Single(notificacao.AlertasAtrasados);
        Assert.Equal([(10, 3), (11, 2)], resumo.Select(a => (a.Pedido.Id, a.DiasAtraso)));
        // Só os itens vencidos entram: o que vence hoje ainda está no prazo.
        Assert.Equal(["Jogo 1", "Jogo 2"], resumo[0].Itens.Select(i => i.JogoCopia.Jogo!.Nome));
        Assert.Equal([1, 2, 4], repo.AlertasAtrasoMarcados);
    }

    [Fact]
    public async Task SemAtrasados_NaoEnviaNada() {
        var (useCase, repo, notificacao) = Criar();
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje));

        Assert.Equal(0, await useCase.ExecuteAsync(AsSeis));
        Assert.Empty(notificacao.AlertasAtrasados);
    }

    [Fact]
    public async Task ItemJaDevolvido_NaoEntra() {
        var (useCase, repo, notificacao) = Criar();
        var pedido = PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje.AddDays(-2));
        pedido.Items[0].Status = StatusPedido.Devolvido;
        repo.Pedidos.Add(pedido);

        await useCase.ExecuteAsync(AsSeis);

        Assert.Empty(notificacao.AlertasAtrasados);
    }

    [Fact]
    public async Task RodarDeNovoNoMesmoDia_NaoRepete_MasNoDiaSeguinteAvisaDeNovo() {
        var (useCase, repo, notificacao) = Criar();
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje.AddDays(-1)));

        await useCase.ExecuteAsync(AsSeis);
        await useCase.ExecuteAsync(AsSeis.AddHours(5));
        Assert.Single(notificacao.AlertasAtrasados);

        await useCase.ExecuteAsync(AsSeis.AddDays(1));
        Assert.Equal(2, notificacao.AlertasAtrasados.Count);
        Assert.Equal(2, notificacao.AlertasAtrasados[1].Single().DiasAtraso);
    }

    [Fact]
    public async Task FalhaNoEnvio_NaoMarcaOsItens() {
        var (useCase, repo, notificacao) = Criar();
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje.AddDays(-1)));
        notificacao.FalharAlertaAtrasados = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(AsSeis));

        Assert.Empty(repo.AlertasAtrasoMarcados);
    }

    [Fact]
    public void Mensagem_ListaOsMaisAtrasadosPrimeiroEJuntaOResto() {
        var atrasados = Enumerable.Range(1, 6)
            .Select(n => {
                var cliente = PedidoTestFactory.Cliente(id: n, email: $"c{n}@teste.com");
                cliente.Nome = $"cliente {n}";
                var pedido = PedidoEntregue(cliente, 100 + n, Hoje.AddDays(-n));
                return new PedidoAtrasado(pedido, pedido.Items, n);
            })
            .ToList();

        var mensagem = NotificationService.MontarMensagemAtrasados(atrasados);

        Assert.Equal("6 pedidos atrasados", mensagem.Titulo);
        Assert.Equal(
            "Cliente 6 · #106 · Jogo 6 · 6 dias\nCliente 5 · #105 · Jogo 5 · 5 dias\nCliente 4 · #104 · Jogo 4 · 4 dias\nCliente 3 · #103 · Jogo 3 · 3 dias\ne mais 2 pedidos",
            mensagem.Corpo);
        Assert.Equal("/pedidos?atrasados=1", mensagem.Url);
    }

    [Fact]
    public void Mensagem_UmPedidoComVariosJogos() {
        var cliente = PedidoTestFactory.Cliente(id: 1, email: "ana@teste.com");
        cliente.Nome = "ana souza";
        var pedido = PedidoEntregue(cliente, 10, Hoje.AddDays(-1), Hoje.AddDays(-1));

        var mensagem = NotificationService.MontarMensagemAtrasados([new PedidoAtrasado(pedido, pedido.Items, 1)]);

        Assert.Equal("1 pedido atrasado", mensagem.Titulo);
        Assert.Equal("Ana Souza · #10 · Jogo 1 e Jogo 2 · 1 dia", mensagem.Corpo);
    }

    [Theory]
    [InlineData("2026-11-20 05:30", 30)]
    [InlineData("2026-11-20 06:00", 24 * 60)]
    [InlineData("2026-11-20 15:00", 15 * 60)]
    public void ProximaExecucao_EhAsSeisDeHojeOuDeAmanha(string agora, int minutos) {
        var espera = AlertaPedidosAtrasadosBackgroundService.TempoAteProximaExecucao(DateTime.Parse(agora));

        Assert.Equal(TimeSpan.FromMinutes(minutos), espera);
    }
}
