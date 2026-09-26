using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Models;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ContratoQueueBackgroundServiceTests
{
    private class FakeContratoQueue : IContratoQueue
    {
        public readonly List<ContratoJob> Jobs = new();
        private readonly SemaphoreSlim _sem = new(0);

        public void Enfileirar(int idPedido, int tentativas = 0, bool inativarExistente = false)
        {
            Jobs.Add(new ContratoJob(idPedido, tentativas, inativarExistente));
            _sem.Release();
        }

        public async ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken)
        {
            await _sem.WaitAsync(cancellationToken);
            var job = Jobs[0];
            if (job.Tentativas > 0)
            {
                // Interrompe o loop do background service para que o job de retentativa
                // permaneça na fila e possa ser verificado pelo teste.
                throw new OperationCanceledException();
            }
            Jobs.RemoveAt(0);
            return job;
        }
    }

    [Fact]
    public async Task ExecuteAsync_QuandoOcorreErro_DeveReenfileirarJobComDelay()
    {
        // Arrange
        var queue = new FakeContratoQueue();
        queue.Enfileirar(42, 0);

        // Simulador do escopo para retornar erro na resolução/execução
        var serviceProvider = new FakeServiceProvider(_ => throw new Exception("Falha de rede Simulada"));
        var scopeFactory = new FakeServiceScopeFactory(serviceProvider);
        
        var service = new ContratoQueueBackgroundService(
            queue, 
            scopeFactory, 
            NullLogger<ContratoQueueBackgroundService>.Instance,
            new[] { TimeSpan.FromMilliseconds(1) } // Delay instantâneo para testes
        );

        using var cts = new CancellationTokenSource();

        // Act
        var runTask = service.StartAsync(cts.Token);
        
        // Aguarda um pequeno momento para processamento
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);
        cts.Cancel();

        // Assert
        Assert.Single(queue.Jobs);
        Assert.Equal(42, queue.Jobs[0].IdPedido);
        Assert.Equal(1, queue.Jobs[0].Tentativas);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoInativarExistenteEhTrue_DeveChamarInativacaoNoRepositorio()
    {
        // Arrange
        var queue = new FakeContratoQueue();
        queue.Enfileirar(42, 0, inativarExistente: true);

        var repo = new FakeContratoRepository();
        var serviceProvider = new FakeServiceProvider(t =>
        {
            if (t == typeof(IContratoRepository)) return repo;
            if (t == typeof(GerarContratoPedido)) throw new OperationCanceledException(); // Interrompe após a inativação
            return null;
        });
        var scopeFactory = new FakeServiceScopeFactory(serviceProvider);
        
        var service = new ContratoQueueBackgroundService(
            queue, 
            scopeFactory, 
            NullLogger<ContratoQueueBackgroundService>.Instance,
            new[] { TimeSpan.FromMilliseconds(1) }
        );

        // Act
        using var cts = new CancellationTokenSource();
        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        // Assert
        Assert.Equal(42, repo.InativarChamadoParaPedidoId);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoContratoAtivoEstaAssinado_NaoInativaNemGeraNovoContrato()
    {
        // Arrange
        var queue = new FakeContratoQueue();
        queue.Enfileirar(42, 0, inativarExistente: true);

        var repo = new FakeContratoRepository
        {
            ContratoAtivo = new ContratoAutentique
            {
                IdPedido = 42,
                AutentiqueDocumentId = "doc-1",
                AutentiquePublicId = "pub-1",
                LinkAssinatura = "https://exemplo.test/assinar",
                Status = StatusContrato.Assinado
            }
        };

        var gerarContratoFoiResolvido = false;
        var serviceProvider = new FakeServiceProvider(t =>
        {
            if (t == typeof(IContratoRepository)) return repo;
            if (t == typeof(GerarContratoPedido))
            {
                gerarContratoFoiResolvido = true;
                throw new OperationCanceledException();
            }
            return null;
        });
        var scopeFactory = new FakeServiceScopeFactory(serviceProvider);

        var service = new ContratoQueueBackgroundService(
            queue,
            scopeFactory,
            NullLogger<ContratoQueueBackgroundService>.Instance,
            new[] { TimeSpan.FromMilliseconds(1) }
        );

        // Act
        using var cts = new CancellationTokenSource();
        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        // Assert
        Assert.Equal(0, repo.InativarChamadoParaPedidoId);
        Assert.False(gerarContratoFoiResolvido);
        // Descartado de forma limpa: nao e tratado como erro, entao nao re-enfileira.
        Assert.Empty(queue.Jobs);
    }

    private class FakeContratoRepository : IContratoRepository
    {
        public int InativarChamadoParaPedidoId { get; private set; }
        public ContratoAutentique? ContratoAtivo { get; set; }
        public Task InativarContratosPorPedidoIdAsync(int idPedido)
        {
            InativarChamadoParaPedidoId = idPedido;
            return Task.CompletedTask;
        }
        public Task SaveAsync(ContratoAutentique contrato, bool commit = true) => Task.CompletedTask;
        public Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) => Task.FromResult(ContratoAtivo);
        public Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string id) => Task.FromResult<ContratoAutentique?>(null);
        public Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos) => Task.FromResult(new List<ContratoAutentique>());
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
        public Task ExcluirPorClienteAsync(int idCliente)
        {
            throw new NotImplementedException();
        }
    }
}

public class FakeServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new FakeServiceScope(serviceProvider);
}

public class FakeServiceScope(IServiceProvider serviceProvider) : IServiceScope
{
    public IServiceProvider ServiceProvider => serviceProvider;
    public void Dispose() { }
}

public class FakeServiceProvider(Func<Type, object?> getService) : IServiceProvider
{
    public object? GetService(Type serviceType) => getService(serviceType);
}
