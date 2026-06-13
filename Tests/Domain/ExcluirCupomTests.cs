using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ExcluirCupomTests
{
    private class FakeCupomRepository : ICupomRepository
    {
        public List<Cupom> Cupons { get; set; } = new();
        public List<Pedido> Pedidos { get; set; } = new();

        public Task<Cupom?> GetByIdAsync(int id) => Task.FromResult(Cupons.FirstOrDefault(c => c.Id == id));

        public Task<Cupom?> GetByCodigoAsync(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return Task.FromResult<Cupom?>(null);
            var normalized = codigo.Trim().ToUpperInvariant();
            return Task.FromResult(Cupons.FirstOrDefault(c => c.Codigo.Trim().ToUpperInvariant() == normalized));
        }

        public Task<List<Cupom>> GetAllAsync(global::ProximoTurnoApi.Application.DTOs.Filtros.FiltroCupomDTO filtro) => throw new NotImplementedException();

        public Task<int> GetUsoCountGlobalAsync(int cupomId, int? idPedidoExcluir = null) => throw new NotImplementedException();

        public Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId, int? idPedidoExcluir = null) => throw new NotImplementedException();

        public Task SaveAsync(Cupom cupom, bool commit = true) => throw new NotImplementedException();

        public Task<bool> DeleteAsync(int id)
        {
            var cupom = Cupons.FirstOrDefault(c => c.Id == id);
            if (cupom == null) return Task.FromResult(false);
            Cupons.Remove(cupom);
            return Task.FromResult(true);
        }

        public Task<bool> IsUsedInPedidoAsync(int id)
        {
            return Task.FromResult(Pedidos.Any(p => p.IdCupom == id));
        }

        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task StartTransactionAsync() => throw new NotImplementedException();
        public Task CommitTransactionAsync() => throw new NotImplementedException();
        public Task RollbackTransactionAsync() => throw new NotImplementedException();
    }

    private class FakeLogger : ILogger<ExcluirCupom>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task ExecuteAsync_WhenCupomDoesNotExist_ShouldReturnFalseAndNotFoundNotification()
    {
        // Arrange
        var repo = new FakeCupomRepository();
        var useCase = new ExcluirCupom(repo, new FakeLogger());

        // Act
        var result = await useCase.ExecuteAsync(999);

        // Assert
        Assert.False(result);
        Assert.False(useCase.IsValid);
        var notification = useCase.Notifications.FirstOrDefault();
        Assert.NotNull(notification);
        Assert.Equal(UseCaseNotificationType.NotFound, notification.Type);
        Assert.Equal("Cupom não encontrado.", notification.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCupomIsUsedInPedido_ShouldReturnFalseAndBadRequestNotification()
    {
        // Arrange
        var cupom = new Cupom { Id = 1, Codigo = "CUPOM10", Ativo = true };
        var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var pedido = new Pedido(cliente) { Id = 100 };
        pedido.AplicarCupom(cupom.Id, 10.0m);

        var repo = new FakeCupomRepository { Cupons = { cupom }, Pedidos = { pedido } };
        var useCase = new ExcluirCupom(repo, new FakeLogger());

        // Act
        var result = await useCase.ExecuteAsync(1);

        // Assert
        Assert.False(result);
        Assert.False(useCase.IsValid);
        var notification = useCase.Notifications.FirstOrDefault();
        Assert.NotNull(notification);
        Assert.Equal(UseCaseNotificationType.BadRequest, notification.Type);
        Assert.Equal("Este cupom já foi utilizado em pedidos e não pode ser excluído.", notification.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCupomExistsAndNotUsed_ShouldDeleteAndReturnTrue()
    {
        // Arrange
        var cupom = new Cupom { Id = 1, Codigo = "CUPOM10", Ativo = true };
        var repo = new FakeCupomRepository { Cupons = { cupom } };
        var useCase = new ExcluirCupom(repo, new FakeLogger());

        // Act
        var result = await useCase.ExecuteAsync(1);

        // Assert
        Assert.True(result);
        Assert.True(useCase.IsValid);
        Assert.Empty(repo.Cupons);
    }
}
