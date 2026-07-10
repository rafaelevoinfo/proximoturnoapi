using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Tests.Domain;

public class EnviarEmailsClientesTests
{
    private static (EnviarEmailsClientes useCase, FakeEmailService email) Montar(
        string? linkResetar = "https://site/resetar-senha?token=RST",
        string? linkAtivar = "https://site/ativar-conta?token=ATV")
    {
        var repo = new FakeClienteRepository
        {
            Clientes = { new Cliente { Id = 1, Nome = "ana", Email = "ana@x.com", Telefone = "6499", Endereco = "rua" } }
        };
        var email = new FakeEmailService();
        var links = new FakeResetSenhaLinkService(linkResetar, linkAtivar);
        var useCase = new EnviarEmailsClientes(repo, email, links, NullLogger<EnviarEmailsClientes>.Instance);
        return (useCase, email);
    }

    private static EnviarEmailsClientesRequest Req(string conteudo) =>
        new() { ClienteIds = [1], Titulo = "Olá {cliente_nome}", Conteudo = conteudo };

    [Fact]
    public async Task Substitui_LinkAtivarConta()
    {
        var (useCase, email) = Montar();

        await useCase.ExecuteAsync(Req("Ative aqui: {link_ativar_conta}"));

        Assert.Single(email.Enviados);
        Assert.Equal("Ative aqui: https://site/ativar-conta?token=ATV", email.Enviados[0].body);
    }

    [Fact]
    public async Task Substitui_AmbosOsLinks_NaMesmaMensagem()
    {
        var (useCase, email) = Montar();

        await useCase.ExecuteAsync(Req("ativar={link_ativar_conta} resetar={link_resetar_senha}"));

        Assert.Equal(
            "ativar=https://site/ativar-conta?token=ATV resetar=https://site/resetar-senha?token=RST",
            email.Enviados[0].body);
    }

    [Fact]
    public async Task Substitui_NomeDoClienteNoTituloENoCorpo()
    {
        var (useCase, email) = Montar();

        await useCase.ExecuteAsync(Req("Oi {cliente_nome}"));

        Assert.Equal("Olá ana", email.Enviados[0].subject);
        Assert.Equal("Oi ana", email.Enviados[0].body);
    }

    [Fact]
    public async Task NaoEnvia_QuandoLinkDeAtivacaoNaoPodeSerGerado()
    {
        var (useCase, email) = Montar(linkAtivar: null);

        var ok = await useCase.ExecuteAsync(Req("Ative: {link_ativar_conta}"));

        Assert.True(ok);                  // o lote não aborta
        Assert.Empty(email.Enviados);     // mas ninguém recebe um botão morto
    }

    [Fact]
    public async Task Envia_SemGerarLinks_QuandoNaoHaPlaceholderDeLink()
    {
        var links = new FakeResetSenhaLinkService("r", "a");
        var repo = new FakeClienteRepository
        {
            Clientes = { new Cliente { Id = 1, Nome = "ana", Email = "ana@x.com", Telefone = "6499", Endereco = "rua" } }
        };
        var email = new FakeEmailService();
        var useCase = new EnviarEmailsClientes(repo, email, links, NullLogger<EnviarEmailsClientes>.Instance);

        await useCase.ExecuteAsync(Req("Apenas um aviso, {cliente_nome}."));

        Assert.Single(email.Enviados);
        Assert.Equal(0, links.Chamadas);
    }

    private class FakeEmailService : IEmailService
    {
        public List<(string to, string subject, string body)> Enviados { get; } = [];

        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            Enviados.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private class FakeResetSenhaLinkService(string? resetar, string? ativar) : IResetSenhaLinkService
    {
        public int Chamadas { get; private set; }

        public Task<string?> GerarLinkAsync(string email, string path = "/resetar-senha")
        {
            Chamadas++;
            return Task.FromResult(path == "/ativar-conta" ? ativar : resetar);
        }
    }

    private class FakeClienteRepository : IClienteRepository
    {
        public List<Cliente> Clientes { get; set; } = [];

        public Task<List<Cliente>> GetAllByIdsAsync(List<int> ids)
            => Task.FromResult(Clientes.Where(c => ids.Contains(c.Id)).ToList());

        public Task<List<Cliente>> GetAllAsync(FiltroClienteDTO filtro) => throw new NotImplementedException();
        public Task<Cliente?> GetByIdAsync(int id) => Task.FromResult(Clientes.FirstOrDefault(c => c.Id == id));
        public Task<int?> GetIdByEmailAsync(string email) => throw new NotImplementedException();
        public Task<Cliente?> GetByEmailAsync(string email) => throw new NotImplementedException();
        public Task AddAsync(Cliente cliente) => throw new NotImplementedException();
        public Task UpdateAsync(Cliente cliente) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(int id) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
    }
}
