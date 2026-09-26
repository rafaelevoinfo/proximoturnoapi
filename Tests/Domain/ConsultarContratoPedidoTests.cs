using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ConsultarContratoPedidoTests
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

        public Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId) => throw new NotImplementedException();
        public Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos) => throw new NotImplementedException();
        public Task InativarContratosPorPedidoIdAsync(int idPedido) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
        public Task ExcluirPorClienteAsync(int idCliente) => throw new NotImplementedException();
    }

    private class FakeClienteRepository : IClienteRepository
    {
        public List<Cliente> Clientes { get; set; } = new();

        public Task<List<Cliente>> GetAllByIdsAsync(List<int> ids)
            => Task.FromResult(Clientes.Where(c => ids.Contains(c.Id)).ToList());

        public Task<int?> GetIdByEmailAsync(string email)
        {
            var cliente = Clientes.FirstOrDefault(c => c.Email == email);
            return Task.FromResult<int?>(cliente?.Id);
        }

        public Task<List<Cliente>> GetAllAsync(global::ProximoTurnoApi.Application.DTOs.FiltroClienteDTO filtro) => throw new NotImplementedException();
        public Task<Cliente?> GetByIdAsync(int id) => Task.FromResult(Clientes.FirstOrDefault(c => c.Id == id));
        public Task<Cliente?> GetByEmailAsync(string email) => Task.FromResult(Clientes.FirstOrDefault(c => c.Email == email));
        public Task AddAsync(Cliente cliente) => throw new NotImplementedException();
        public Task UpdateAsync(Cliente cliente) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(int id) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
        public Task ExcluirDadosVinculadosAsync(int idCliente) => throw new NotImplementedException();
    }

    private class FakePedidoRepository : IPedidoRepository
    {
        public List<Pedido> Pedidos { get; set; } = new();

        public Task<Pedido?> GetByIdAsync(int id)
        {
            return Task.FromResult(Pedidos.FirstOrDefault(p => p.Id == id));
        }

        public Task SaveAsync(Pedido pedido, bool commit = true) => throw new NotImplementedException();
        public Task<List<Pedido>> GetAllAsync(global::ProximoTurnoApi.Application.DTOs.FiltroPedidoDTO filtro) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
        public Task<List<Pedido>> ObterPedidosEmAbertoAsync(int idCliente) => throw new NotImplementedException();
    }

    private class FakeAutentiqueService : IAutentiqueService
    {
        public AutentiqueDocumentStatus? Status { get; set; }
        public bool ConsultarCalled { get; private set; }

        public Task<AutentiqueDocumentStatus> ConsultarDocumentoAsync(string documentId)
        {
            ConsultarCalled = true;
            if (Status == null) throw new InvalidOperationException("Status is not set");
            return Task.FromResult(Status);
        }

        public Task<AutentiqueDocumentResult> CriarDocumentoAsync(byte[] pdfBytes, string nomeDocumento, string nomeSignatario, bool sandbox) => throw new NotImplementedException();
        public Task<string> ObterLinkAssinaturaAsync(string publicId) => throw new NotImplementedException();
    }

    private class FakeUserManager : UserManager<Usuario>
    {
        private readonly Usuario? _userToReturn;

        public FakeUserManager(Usuario? userToReturn) : base(
            new FakeUserStore(),
            null!, null!, null!, null!, null!, null!, null!, null!)
        {
            _userToReturn = userToReturn;
        }

        public override Task<Usuario?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult(_userToReturn);
        }
    }

    private class FakeUserStore : IUserStore<Usuario>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<string?> GetUserNameAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SetUserNameAsync(Usuario user, string? userName, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<string?> GetNormalizedUserNameAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SetNormalizedUserNameAsync(Usuario user, string? normalizedName, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IdentityResult> CreateAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IdentityResult> UpdateAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IdentityResult> DeleteAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Usuario?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Usuario?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private class FakeLogger : ILogger<ConsultarContratoPedido>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task ExecuteAsync_WhenContratoDoesNotExist_ShouldReturnNullAndNotFoundNotification()
    {
        // Arrange
        var contratoRepo = new FakeContratoRepository();
        var autentiqueService = new FakeAutentiqueService();
        var userManager = new FakeUserManager(null);
        var clienteRepo = new FakeClienteRepository();
        var pedidoRepo = new FakePedidoRepository();
        var logger = new FakeLogger();

        var useCase = new ConsultarContratoPedido(contratoRepo, autentiqueService, userManager, clienteRepo, pedidoRepo, logger);
        var userClaim = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await useCase.ExecuteAsync(userClaim, 999);

        // Assert
        Assert.Null(result);
        Assert.False(useCase.IsValid);
        var notification = useCase.Notifications.FirstOrDefault();
        Assert.NotNull(notification);
        Assert.Equal(UseCaseNotificationType.NotFound, notification.Type);
        Assert.Equal("Nenhum contrato encontrado para este pedido.", notification.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsAdmin_ShouldBypassOwnershipCheckAndReturnContrato()
    {
        // Arrange
        var cliente = new Cliente { Id = 10, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var pedido = new Pedido(cliente) { Id = 100 };
        var contrato = new ContratoAutentique 
        { 
            Id = 1, 
            IdPedido = 100, 
            Status = StatusContrato.Assinado, 
            Ativo = true, 
            Pedido = pedido,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var contratoRepo = new FakeContratoRepository { Contratos = { contrato } };
        var autentiqueService = new FakeAutentiqueService();
        var userManager = new FakeUserManager(null); // Return null user to check it doesn't fail
        var clienteRepo = new FakeClienteRepository();
        var pedidoRepo = new FakePedidoRepository();
        var logger = new FakeLogger();

        var useCase = new ConsultarContratoPedido(contratoRepo, autentiqueService, userManager, clienteRepo, pedidoRepo, logger);

        // Claims principal representing Admin
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Admin) });
        var userClaim = new ClaimsPrincipal(identity);

        // Act
        var result = await useCase.ExecuteAsync(userClaim, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(useCase.IsValid);
        Assert.Equal(contrato, result);
        Assert.False(autentiqueService.ConsultarCalled); // Already signed, not pending
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsOwner_ShouldAllowAccess()
    {
        // Arrange
        var cliente = new Cliente { Id = 10, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var pedido = new Pedido(cliente) { Id = 100 };
        var contrato = new ContratoAutentique 
        { 
            Id = 1, 
            IdPedido = 100, 
            Status = StatusContrato.Assinado, 
            Ativo = true, 
            Pedido = pedido,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var contratoRepo = new FakeContratoRepository { Contratos = { contrato } };
        var autentiqueService = new FakeAutentiqueService();
        
        var usuario = new Usuario { Email = "teste@teste.com" };
        var userManager = new FakeUserManager(usuario);
        
        var clienteRepo = new FakeClienteRepository { Clientes = { cliente } };
        var pedidoRepo = new FakePedidoRepository { Pedidos = { pedido } };
        var logger = new FakeLogger();

        var useCase = new ConsultarContratoPedido(contratoRepo, autentiqueService, userManager, clienteRepo, pedidoRepo, logger);

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Member) });
        var userClaim = new ClaimsPrincipal(identity);

        // Act
        var result = await useCase.ExecuteAsync(userClaim, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(useCase.IsValid);
        Assert.Equal(contrato, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotOwnerAndNotAdmin_ShouldDenyAccessAndReturnForbidNotification()
    {
        // Arrange
        var cliente = new Cliente { Id = 10, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var pedido = new Pedido(cliente) { Id = 100 };
        var contrato = new ContratoAutentique 
        { 
            Id = 1, 
            IdPedido = 100, 
            Status = StatusContrato.Assinado, 
            Ativo = true, 
            Pedido = pedido,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "link123"
        };

        var contratoRepo = new FakeContratoRepository { Contratos = { contrato } };
        var autentiqueService = new FakeAutentiqueService();
        
        var usuario = new Usuario { Email = "other@teste.com" };
        var userManager = new FakeUserManager(usuario);
        
        var clienteRepo = new FakeClienteRepository { Clientes = { new Cliente { Id = 11, Email = "other@teste.com", Nome = "Other Cliente", Telefone = "123456", Endereco = "Rua Outra" } } };
        var pedidoRepo = new FakePedidoRepository { Pedidos = { pedido } };
        var logger = new FakeLogger();

        var useCase = new ConsultarContratoPedido(contratoRepo, autentiqueService, userManager, clienteRepo, pedidoRepo, logger);

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Member) });
        var userClaim = new ClaimsPrincipal(identity);

        // Act
        var result = await useCase.ExecuteAsync(userClaim, 100);

        // Assert
        Assert.Null(result);
        Assert.False(useCase.IsValid);
        var notification = useCase.Notifications.FirstOrDefault();
        Assert.NotNull(notification);
        Assert.Equal(UseCaseNotificationType.Forbid, notification.Type);
        Assert.Equal("Acesso negado: você não tem permissão para visualizar o contrato deste pedido.", notification.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContratoIsPendenteAndSignedAtAutentique_ShouldUpdateStatusToAssinado()
    {
        // Arrange
        var cliente = new Cliente { Id = 10, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var pedido = new Pedido(cliente) { Id = 100 };
        var contrato = new ContratoAutentique 
        { 
            Id = 1, 
            IdPedido = 100, 
            Status = StatusContrato.Pendente, 
            Ativo = true, 
            Pedido = pedido,
            AutentiqueDocumentId = "doc123",
            AutentiquePublicId = "pub123",
            LinkAssinatura = "old_link"
        };

        var contratoRepo = new FakeContratoRepository { Contratos = { contrato } };
        var autentiqueService = new FakeAutentiqueService 
        { 
            Status = new AutentiqueDocumentStatus("doc123", SignedAt: "2026-06-18T00:00:00Z", RejectedAt: null, ViewedAt: null, SigningLink: "new_link") 
        };
        
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Admin) });
        var userClaim = new ClaimsPrincipal(identity);
        var userManager = new FakeUserManager(null);
        var clienteRepo = new FakeClienteRepository();
        var pedidoRepo = new FakePedidoRepository();
        var logger = new FakeLogger();

        var useCase = new ConsultarContratoPedido(contratoRepo, autentiqueService, userManager, clienteRepo, pedidoRepo, logger);

        // Act
        var result = await useCase.ExecuteAsync(userClaim, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(useCase.IsValid);
        Assert.Equal(StatusContrato.Assinado, result.Status);
        Assert.Equal("new_link", result.LinkAssinatura);
        Assert.True(contratoRepo.SaveCalled);
    }
}
