using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ProcessarWebhookAutentiqueTests
{
    private class FakeContratoRepository : IContratoRepository
    {
        public List<ContratoAutentique> Contratos { get; set; } = new();
        public bool SaveCalled { get; private set; }

        public Task SaveAsync(ContratoAutentique contrato, bool commit = true)
        {
            SaveCalled = true;
            var existing = Contratos.FirstOrDefault(c => c.Id == contrato.Id);
            if (existing != null)
            {
                Contratos.Remove(existing);
            }
            Contratos.Add(contrato);
            return Task.CompletedTask;
        }

        public Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido)
        {
            return Task.FromResult(Contratos.FirstOrDefault(c => c.IdPedido == idPedido && c.Ativo));
        }

        public Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId)
        {
            return Task.FromResult(Contratos.FirstOrDefault(c => c.AutentiqueDocumentId == autentiqueDocumentId && c.Ativo));
        }

        public Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos) => throw new NotImplementedException();
        public Task InativarContratosPorPedidoIdAsync(int idPedido) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
    }

    private class FakeLogger : ILogger<ProcessarWebhookAutentique>
    {
        public List<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenSignatureAccepted_ShouldUpdateStatusToAssinadoAndSetDataAssinatura()
    {
        // Arrange
        var contrato = new ContratoAutentique
        {
            Id = 1,
            IdPedido = 100,
            Status = StatusContrato.Pendente,
            Ativo = true,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var repo = new FakeContratoRepository { Contratos = { contrato } };
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""signature.accepted"",
                ""data"": {
                    ""document"": ""doc123""
                }
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.True(repo.SaveCalled);
        Assert.Equal(StatusContrato.Assinado, contrato.Status);
        Assert.NotNull(contrato.DataAssinatura);
        Assert.True((DateTime.Now - contrato.DataAssinatura.Value).TotalSeconds < 5);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentFinished_ShouldUpdateStatusToAssinadoAndSetDataAssinatura()
    {
        // Arrange
        var contrato = new ContratoAutentique
        {
            Id = 1,
            IdPedido = 100,
            Status = StatusContrato.Pendente,
            Ativo = true,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var repo = new FakeContratoRepository { Contratos = { contrato } };
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""document.finished"",
                ""data"": {
                    ""id"": ""doc123""
                }
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.True(repo.SaveCalled);
        Assert.Equal(StatusContrato.Assinado, contrato.Status);
        Assert.NotNull(contrato.DataAssinatura);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSignatureRejected_ShouldUpdateStatusToRejeitado()
    {
        // Arrange
        var contrato = new ContratoAutentique
        {
            Id = 1,
            IdPedido = 100,
            Status = StatusContrato.Pendente,
            Ativo = true,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var repo = new FakeContratoRepository { Contratos = { contrato } };
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""signature.rejected"",
                ""data"": {
                    ""document"": ""doc123""
                }
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.True(repo.SaveCalled);
        Assert.Equal(StatusContrato.Rejeitado, contrato.Status);
        Assert.Null(contrato.DataAssinatura);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContratoIsNotPendente_ShouldIgnoreWebhook()
    {
        // Arrange
        var contrato = new ContratoAutentique
        {
            Id = 1,
            IdPedido = 100,
            Status = StatusContrato.Assinado,
            Ativo = true,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123",
            DataAssinatura = DateTime.Now.AddDays(-1)
        };

        var repo = new FakeContratoRepository { Contratos = { contrato } };
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""signature.accepted"",
                ""data"": {
                    ""document"": ""doc123""
                }
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.False(repo.SaveCalled);
        Assert.Contains("ignorando webhook", logger.LoggedMessages.LastOrDefault() ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_WhenContratoNotFound_ShouldLogWarningAndReturn()
    {
        // Arrange
        var repo = new FakeContratoRepository();
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""signature.accepted"",
                ""data"": {
                    ""document"": ""doc123""
                }
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.False(repo.SaveCalled);
        Assert.Contains("Contrato não encontrado", logger.LoggedMessages.Any(m => m.Contains("Contrato não encontrado")) ? logger.LoggedMessages.First(m => m.Contains("Contrato não encontrado")) : "");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadHasNoEventObject_ShouldLogWarningAndReturn()
    {
        // Arrange
        var repo = new FakeContratoRepository();
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"{ ""something"": ""else"" }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.False(repo.SaveCalled);
        Assert.Contains("Webhook recebido sem objeto 'event'", logger.LoggedMessages.Any(m => m.Contains("Webhook recebido sem objeto")) ? logger.LoggedMessages.First(m => m.Contains("Webhook recebido sem objeto")) : "");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadHasNoDocumentId_ShouldLogWarningAndReturn()
    {
        // Arrange
        var repo = new FakeContratoRepository();
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = @"
        {
            ""event"": {
                ""type"": ""signature.accepted"",
                ""data"": {}
            }
        }";

        // Act
        await useCase.ExecuteAsync(payload);

        // Assert
        Assert.False(repo.SaveCalled);
        Assert.Contains("Webhook recebido sem document ID identificável", logger.LoggedMessages.Any(m => m.Contains("sem document ID")) ? logger.LoggedMessages.First(m => m.Contains("sem document ID")) : "");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsonIsInvalid_ShouldLogErrorAndGracefullyHandle()
    {
        // Arrange
        var repo = new FakeContratoRepository();
        var logger = new FakeLogger();
        var useCase = new ProcessarWebhookAutentique(repo, logger);

        var payload = "{ invalid json }";

        // Act & Assert (Should not throw exception)
        var exception = await Record.ExceptionAsync(() => useCase.ExecuteAsync(payload));
        Assert.Null(exception);
        Assert.Contains("Erro ao parsear payload do webhook", logger.LoggedMessages.Any(m => m.Contains("Erro ao parsear")) ? logger.LoggedMessages.First(m => m.Contains("Erro ao parsear")) : "");
    }
}
