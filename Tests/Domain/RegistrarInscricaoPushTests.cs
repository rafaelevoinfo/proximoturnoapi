using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RegistrarInscricaoPushTests {
    private static (RegistrarInscricaoPush, FakeInscricaoPushRepository) Criar() {
        var repo = new FakeInscricaoPushRepository();
        return (new RegistrarInscricaoPush(repo, NullLogger<RegistrarInscricaoPush>.Instance), repo);
    }

    private static InscricaoPushDTO Dto(string endpoint = "https://fcm.googleapis.com/fcm/send/abc") => new() {
        Endpoint = endpoint, P256dh = "chave-p256dh", Auth = "chave-auth"
    };

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("/relativo")]
    [InlineData("")]
    public async Task RejeitaEndpointQueNaoEhHttps(string endpoint) {
        var (useCase, repo) = Criar();

        var ok = await useCase.ExecuteAsync("admin", Dto(endpoint), null);

        Assert.False(ok);
        Assert.Empty(repo.Inscricoes);
    }

    [Fact]
    public async Task RejeitaInscricaoSemChaves() {
        var (useCase, repo) = Criar();

        var ok = await useCase.ExecuteAsync("admin", Dto() with { Auth = "" }, null);

        Assert.False(ok);
        Assert.Empty(repo.Inscricoes);
    }

    [Fact]
    public async Task CriaInscricaoNova() {
        var (useCase, repo) = Criar();

        var ok = await useCase.ExecuteAsync("admin", Dto(), "Mozilla/5.0 (iPhone)");

        Assert.True(ok);
        var inscricao = Assert.Single(repo.Inscricoes);
        Assert.Equal("admin", inscricao.IdUsuario);
        Assert.Equal("chave-p256dh", inscricao.P256dh);
        Assert.Equal("Mozilla/5.0 (iPhone)", inscricao.UserAgent);
    }

    [Fact]
    public async Task MesmoEndpoint_AtualizaEmVezDeDuplicar_EPassaParaQuemLogou() {
        var (useCase, repo) = Criar();
        await useCase.ExecuteAsync("admin1", Dto(), null);

        var ok = await useCase.ExecuteAsync("admin2", Dto() with { Auth = "nova-auth" }, null);

        Assert.True(ok);
        var inscricao = Assert.Single(repo.Inscricoes);
        Assert.Equal("admin2", inscricao.IdUsuario);
        Assert.Equal("nova-auth", inscricao.Auth);
        Assert.Equal(2, repo.Salvamentos);
    }
}
