using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EnviarLembretesDevolucaoTests {
    private static readonly DateTime Hoje = new(2026, 11, 20);
    private static readonly DateTime AsDez = Hoje.AddHours(10);
    private int _proximoIdItem = 1;

    private static (EnviarLembretesDevolucao, FakePedidoRepository, FakeNotificationService) Criar() {
        var repo = new FakePedidoRepository();
        var notificacao = new FakeNotificationService();
        return (new EnviarLembretesDevolucao(repo, notificacao, NullLogger<EnviarLembretesDevolucao>.Instance), repo, notificacao);
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
    public async Task AvisaSoOsItensQueVencemHoje_UmaVezPorCliente() {
        var (useCase, repo, notificacao) = Criar();
        var ana = PedidoTestFactory.Cliente(id: 1, email: "ana@teste.com");
        var bia = PedidoTestFactory.Cliente(id: 2, email: "bia@teste.com");
        repo.Pedidos.Add(PedidoEntregue(ana, 10, Hoje, Hoje.AddDays(3)));
        repo.Pedidos.Add(PedidoEntregue(ana, 11, Hoje));
        repo.Pedidos.Add(PedidoEntregue(bia, 12, Hoje.AddDays(-1)));

        var avisados = await useCase.ExecuteAsync(AsDez);

        Assert.Equal(1, avisados);
        var (cliente, itens) = Assert.Single(notificacao.Lembretes);
        Assert.Equal(1, cliente.Id);
        Assert.Equal(["Jogo 1", "Jogo 3"], itens.Select(i => i.JogoCopia.Jogo!.Nome));
        Assert.Equal([1, 3], repo.LembretesMarcados);
    }

    [Fact]
    public async Task ItemJaDevolvido_NaoEhLembrado() {
        var (useCase, repo, notificacao) = Criar();
        var pedido = PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje);
        pedido.Items[0].Status = StatusPedido.Devolvido;
        repo.Pedidos.Add(pedido);

        await useCase.ExecuteAsync(AsDez);

        Assert.Empty(notificacao.Lembretes);
    }

    [Fact]
    public async Task RodarDeNovoNoMesmoDia_NaoRepeteOLembrete() {
        var (useCase, repo, notificacao) = Criar();
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje));

        await useCase.ExecuteAsync(AsDez);
        await useCase.ExecuteAsync(AsDez.AddHours(3));

        Assert.Single(notificacao.Lembretes);
    }

    [Fact]
    public async Task LembreteDeOutroDia_NaoImpedeOAvisoDeHoje() {
        var (useCase, repo, notificacao) = Criar();
        var pedido = PedidoEntregue(PedidoTestFactory.Cliente(), 10, Hoje);
        // A data de devolução foi adiada depois de um lembrete anterior.
        pedido.Items[0].DataLembreteDevolucao = Hoje.AddDays(-2).AddHours(10);
        repo.Pedidos.Add(pedido);

        await useCase.ExecuteAsync(AsDez);

        Assert.Single(notificacao.Lembretes);
    }

    [Fact]
    public async Task FalhaComUmCliente_NaoMarcaEleENaoImpedeOsOutros() {
        var (useCase, repo, notificacao) = Criar();
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(id: 1, email: "ana@teste.com"), 10, Hoje));
        repo.Pedidos.Add(PedidoEntregue(PedidoTestFactory.Cliente(id: 2, email: "bia@teste.com"), 11, Hoje));
        notificacao.FalharLembreteDoCliente = 1;

        var avisados = await useCase.ExecuteAsync(AsDez);

        Assert.Equal(1, avisados);
        Assert.Equal(2, Assert.Single(notificacao.Lembretes).Cliente.Id);
        Assert.Equal([2], repo.LembretesMarcados);
    }

    [Theory]
    [InlineData("2026-11-20 08:30", 90)]
    [InlineData("2026-11-20 10:00", 24 * 60)]
    [InlineData("2026-11-20 15:00", 19 * 60)]
    public void ProximaExecucao_EhAsDezDeHojeOuDeAmanha(string agora, int minutos) {
        var espera = LembreteDevolucaoBackgroundService.TempoAteProximaExecucao(DateTime.Parse(agora));

        Assert.Equal(TimeSpan.FromMinutes(minutos), espera);
    }
}
