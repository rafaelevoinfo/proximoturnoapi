using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PushServiceTests {
    private static readonly ConfiguracaoPush ComChaves = new("publica", "privada", "mailto:teste@teste.com");
    private static readonly MensagemPush Mensagem = new("Novo pedido #7", "Fulano · 1 jogo · R$ 10,00", "/pedidos?ver=7", "pedido-7");

    private static InscricaoPush Inscricao(string idUsuario, string endpoint) => new() {
        IdUsuario = idUsuario, Endpoint = endpoint, P256dh = "p256dh", Auth = "auth"
    };

    private static (PushService, FakeInscricaoPushRepository, FakeEnviadorWebPush) Criar(ConfiguracaoPush configuracao) {
        var repo = new FakeInscricaoPushRepository();
        var enviador = new FakeEnviadorWebPush();
        return (new PushService(repo, enviador, configuracao, NullLogger<PushService>.Instance), repo, enviador);
    }

    [Fact]
    public async Task SemChavesVapid_NaoEnviaNada() {
        var (service, repo, enviador) = Criar(new ConfiguracaoPush(null, null, "mailto:x@x.com"));
        repo.Admins.Add("admin");
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/1"));

        await service.EnviarParaAdminsAsync(Mensagem);

        Assert.Empty(enviador.Enviados);
    }

    [Fact]
    public async Task EnviaParaCadaAparelhoDosAdmins_ComPayloadQueOServiceWorkerLe() {
        var (service, repo, enviador) = Criar(ComChaves);
        repo.Admins.Add("admin");
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/celular"));
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/notebook"));
        await repo.SalvarAsync(Inscricao("cliente", "https://push.exemplo/cliente"));

        await service.EnviarParaAdminsAsync(Mensagem);

        Assert.Equal(["https://push.exemplo/celular", "https://push.exemplo/notebook"],
                     enviador.Enviados.Select(e => e.Inscricao.Endpoint));
        var payload = JsonDocument.Parse(enviador.Enviados[0].Payload).RootElement;
        Assert.Equal("Novo pedido #7", payload.GetProperty("title").GetString());
        Assert.Equal("Fulano · 1 jogo · R$ 10,00", payload.GetProperty("body").GetString());
        Assert.Equal("/pedidos?ver=7", payload.GetProperty("url").GetString());
        Assert.Equal("pedido-7", payload.GetProperty("tag").GetString());
    }

    [Fact]
    public async Task InscricaoExpirada_EhRemovida_EFalhaComum_Fica() {
        var (service, repo, enviador) = Criar(ComChaves);
        repo.Admins.Add("admin");
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/expirada"));
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/instavel"));
        await repo.SalvarAsync(Inscricao("admin", "https://push.exemplo/ok"));
        enviador.Resultados["https://push.exemplo/expirada"] = ResultadoEnvioPush.InscricaoExpirada;
        enviador.Resultados["https://push.exemplo/instavel"] = ResultadoEnvioPush.Falha;

        await service.EnviarParaAdminsAsync(Mensagem);

        Assert.Equal(3, enviador.Enviados.Count);
        Assert.Equal(["https://push.exemplo/instavel", "https://push.exemplo/ok"],
                     repo.Inscricoes.Select(i => i.Endpoint));
    }
}
