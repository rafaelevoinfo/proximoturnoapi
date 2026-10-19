using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class NotificationServiceTests {
    private static NotificationService Criar(FakeEmailService email, FakePushService push, Dictionary<string, string?> config) =>
        new(email, push, new ConfigurationBuilder().AddInMemoryCollection(config).Build(), NullLogger<NotificationService>.Instance);

    private static Pedido NovoPedido() {
        var cliente = new Cliente { Id = 10, Nome = "Fulano", Email = "fulano@teste.com", Telefone = "123", Endereco = "Rua" };
        var pedido = new Pedido(cliente) { Id = 42 };
        return pedido;
    }

    [Fact]
    public async Task NovoPedido_EnviaPushComLinkParaOPedido() {
        var email = new FakeEmailService();
        var push = new FakePushService();
        var service = Criar(email, push, new());

        await service.EnviarNotificacaoNovoPedidoAsync(NovoPedido());

        var mensagem = Assert.Single(push.Mensagens);
        Assert.Equal("Novo pedido #42", mensagem.Titulo);
        Assert.StartsWith("Fulano · 0 jogos · ", mensagem.Corpo);
        Assert.Equal("/pedidos?ver=42", mensagem.Url);
        Assert.Equal("pedido-42", mensagem.Tag);
        Assert.Single(email.Destinatarios);
    }

    [Fact]
    public async Task PushDesligadoNaConfiguracao_NaoEnviaPush() {
        var push = new FakePushService();
        var service = Criar(new FakeEmailService(), push, new() { ["Notification:Push:Enabled"] = "false" });

        await service.EnviarNotificacaoNovoPedidoAsync(NovoPedido());

        Assert.Empty(push.Mensagens);
    }

    [Fact]
    public async Task CanceladoPeloAdmin_AvisaSoOCliente() {
        var push = new FakePushService();
        var service = Criar(new FakeEmailService(), push, new());

        await service.EnviarNotificacaoPedidoCanceladoAsync(NovoPedido(), canceladoPeloAdmin: true);

        Assert.Empty(push.Mensagens);
        var (email, mensagem) = Assert.Single(push.ParaClientes);
        Assert.Equal("fulano@teste.com", email);
        Assert.Equal("Pedido #42 cancelado", mensagem.Titulo);
        Assert.Equal("/pedidos?ver=42", mensagem.Url);
    }

    [Fact]
    public async Task CanceladoPeloCliente_AvisaSoOsAdmins() {
        var push = new FakePushService();
        var service = Criar(new FakeEmailService(), push, new());

        await service.EnviarNotificacaoPedidoCanceladoAsync(NovoPedido(), canceladoPeloAdmin: false);

        Assert.Empty(push.ParaClientes);
        var mensagem = Assert.Single(push.Mensagens);
        Assert.Equal("Pedido #42 cancelado pelo cliente", mensagem.Titulo);
        Assert.StartsWith("Fulano cancelou o pedido #42", mensagem.Corpo);
    }

    [Fact]
    public async Task LembreteDevolucao_ListaOsJogosParaOCliente() {
        var push = new FakePushService();
        var service = Criar(new FakeEmailService(), push, new());
        var cliente = new Cliente { Id = 10, Nome = "Fulano", Email = "fulano@teste.com", Telefone = "123", Endereco = "Rua" };
        ItemPedido Item(string nome) => new() { JogoCopia = new JogoCopia { Jogo = new Jogo { Nome = nome } } };

        await service.EnviarLembreteDevolucaoAsync(cliente, [Item("Catan"), Item("Azul"), Item("Dixit")]);

        var (email, mensagem) = Assert.Single(push.ParaClientes);
        Assert.Equal("fulano@teste.com", email);
        Assert.Equal("Hoje é dia de devolução", mensagem.Titulo);
        Assert.Equal("Olá, só passando pra lembrar da devolução de Catan, Azul e Dixit hoje, mas fica tranquilo: você tem até as 22:00, então ainda dá pra jogar mais umas partidas :)", mensagem.Corpo);
        Assert.Equal("/pedidos", mensagem.Url);
    }

    [Fact]
    public async Task PushDesligado_NaoEnviaCancelamentoNemLembrete() {
        var push = new FakePushService();
        var service = Criar(new FakeEmailService(), push, new() { ["Notification:Push:Enabled"] = "false" });
        var cliente = new Cliente { Id = 10, Nome = "Fulano", Email = "fulano@teste.com", Telefone = "123", Endereco = "Rua" };

        await service.EnviarNotificacaoPedidoCanceladoAsync(NovoPedido(), canceladoPeloAdmin: true);
        await service.EnviarLembreteDevolucaoAsync(cliente, [new ItemPedido()]);

        Assert.Empty(push.ParaClientes);
        Assert.Empty(push.Mensagens);
    }
}
