using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class StatusLojaTests {
    [Fact]
    public async Task Obter_SemConfiguracaoSalva_LojaAberta() {
        var status = await new ObterStatusLoja(new FakeConfiguracaoLojaRepository()).ExecuteAsync();

        Assert.False(status.LojaFechada);
        Assert.Null(status.Mensagem);
    }

    [Fact]
    public async Task Atualizar_FecharSemMensagem_UsaAMensagemPadrao() {
        var repo = new FakeConfiguracaoLojaRepository();
        var useCase = new AtualizarStatusLoja(repo, new FakeLogger<AtualizarStatusLoja>());

        var status = await useCase.ExecuteAsync(new AtualizarStatusLojaDTO { LojaFechada = true, Mensagem = "   " });

        Assert.True(useCase.IsValid);
        Assert.True(status!.LojaFechada);
        Assert.Equal(StatusLojaDTO.MensagemPadrao, status.Mensagem);
        Assert.Null(status.MensagemPersonalizada);
        Assert.Null(repo.Configuracao!.MensagemLojaFechada);
    }

    [Fact]
    public async Task Atualizar_ReaproveitaALinhaExistenteEGuardaAMensagem() {
        var existente = new ConfiguracaoLoja { Id = 1, LojaFechada = false };
        var repo = new FakeConfiguracaoLojaRepository { Configuracao = existente };
        var useCase = new AtualizarStatusLoja(repo, new FakeLogger<AtualizarStatusLoja>());

        var status = await useCase.ExecuteAsync(new AtualizarStatusLojaDTO { LojaFechada = true, Mensagem = " Férias até dia 10 " });

        Assert.Same(existente, repo.Configuracao);
        Assert.Equal("Férias até dia 10", status!.Mensagem);
    }

    [Fact]
    public async Task Obter_LojaAbertaComMensagemSalva_NaoExibeAviso() {
        var repo = new FakeConfiguracaoLojaRepository { Configuracao = new ConfiguracaoLoja { Id = 1, LojaFechada = false, MensagemLojaFechada = "Férias" } };

        var status = await new ObterStatusLoja(repo).ExecuteAsync();

        Assert.False(status.LojaFechada);
        Assert.Null(status.Mensagem);
        Assert.Equal("Férias", status.MensagemPersonalizada);
    }
}
