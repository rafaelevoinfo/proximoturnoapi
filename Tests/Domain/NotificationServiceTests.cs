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
}
