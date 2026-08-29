using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Tests.Fakes;

/// <summary>Logger no-op reutilizável para qualquer use case.</summary>
public class FakeLogger<T> : ILogger<T> {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

/// <summary>Ambiente de host reutilizável para qualquer use case; por padrão simula produção (fora do fluxo de dev).</summary>
public class FakeHostEnvironment : IHostEnvironment {
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class FakePedidoRepository : IPedidoRepository {
    public List<Pedido> Pedidos { get; set; } = [];
    public List<Pedido> Salvos { get; } = [];
    public int SaveCount { get; private set; }
    private int _proximoId = 1000;

    public Task<Pedido?> GetByIdAsync(int id) => Task.FromResult(Pedidos.FirstOrDefault(p => p.Id == id));

    public Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro) {
        var query = Pedidos.AsEnumerable();
        if (filtro.IdCliente.HasValue) {
            query = query.Where(p => p.Cliente?.Id == filtro.IdCliente.Value);
        }
        // Espelha o PedidoRepository: cada situacao marcada e um ramo do OU, e
        // "Entregue" nao inclui os vencidos (esses vem por Atrasados).
        var querPendente = filtro.Status?.Contains(StatusPedido.Pendente) == true;
        var querEntregue = filtro.Status?.Contains(StatusPedido.Entregue) == true;
        var querDevolvido = filtro.Status?.Contains(StatusPedido.Devolvido) == true;
        var querCancelado = filtro.Status?.Contains(StatusPedido.Cancelado) == true;

        if (querPendente || querEntregue || querDevolvido || querCancelado || filtro.Atrasados) {
            var hoje = DateTime.Today;
            bool EstaAtrasado(Pedido p) =>
                p.Items?.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < hoje) == true;

            query = query.Where(p =>
                (querPendente && p.Status == StatusPedido.Pendente) ||
                (querDevolvido && p.Status == StatusPedido.Devolvido) ||
                (querCancelado && p.Status == StatusPedido.Cancelado) ||
                (querEntregue && p.Status == StatusPedido.Entregue && !EstaAtrasado(p)) ||
                (filtro.Atrasados && EstaAtrasado(p)));
        }
        return Task.FromResult(query.ToList());
    }

    public Task SaveAsync(Pedido pedido, bool commit = true) {
        SaveCount++;
        if (pedido.Id == 0) {
            pedido.Id = _proximoId++;
        }
        if (!Pedidos.Contains(pedido)) {
            Pedidos.Add(pedido);
        }
        Salvos.Add(pedido);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id) {
        var pedido = Pedidos.FirstOrDefault(p => p.Id == id);
        if (pedido is null) return Task.FromResult(false);
        Pedidos.Remove(pedido);
        return Task.FromResult(true);
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

public class FakeCategoriaPeriodoCache : ICategoriaPeriodoCache {
    private readonly Dictionary<int, CategoriaPeriodoInfo> _periodos = [];

    public FakeCategoriaPeriodoCache Adicionar(int idPeriodo, int quantidadeDias, decimal valor, int idCategoria) {
        _periodos[idPeriodo] = new CategoriaPeriodoInfo(idPeriodo, quantidadeDias, valor, idCategoria, "categoria");
        return this;
    }

    public bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info) => _periodos.TryGetValue(idPeriodo, out info);

    public int GetQuantidadeDias(int idPeriodo, int defaultDias = 1) =>
        _periodos.TryGetValue(idPeriodo, out var info) ? info.QuantidadeDias : defaultDias;

    public Task RefreshAsync() => Task.CompletedTask;
}

public class FakeJogoRepository : IJogoRepository {
    public Task<List<JogoLink>> GetJogosNaoIndexadosAsync(int? quantidade = null) => throw new NotImplementedException();
    public Task MarcarIndexadoAsync(int idJogoLink) => throw new NotImplementedException();
    public List<JogoCopia> Copias { get; set; } = [];
    public List<Jogo> Jogos { get; set; } = [];

    public Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo) =>
        Task.FromResult(Copias.Where(c => c.IdJogo == idJogo).ToList());

    public Task<Jogo?> GetResumoByIdAsync(int id) => Task.FromResult(Jogos.FirstOrDefault(j => j.Id == id));
    public Task<Jogo?> GetByIdAsync(int id) => Task.FromResult(Jogos.FirstOrDefault(j => j.Id == id));

    public Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) => throw new NotImplementedException();
    public Task<List<Jogo>> GetMaisAlugadosAsync() => throw new NotImplementedException();
    public Task<List<Jogo>> GetNovidadesAsync(int quantidade = 3) => throw new NotImplementedException();
    public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) => throw new NotImplementedException();
    public Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids) => throw new NotImplementedException();
    public Task<List<JogoCopia>> GetCopiasAsync(int id) => throw new NotImplementedException();
    public Task<JogoCopia?> GetCopiaByIdAsync(int id) => throw new NotImplementedException();
    public Task SaveAsync(Jogo jogo, bool commit = true) => throw new NotImplementedException();
    public Task SaveAsync(JogoCopia jogo, bool commit = true) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
    public Task<bool> ExisteAsync(int id) => throw new NotImplementedException();
    public Task<bool> CopiaExisteAndDisponivel(int id) => throw new NotImplementedException();
    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

public class FakeClienteRepository : IClienteRepository {
    public List<Cliente> Clientes { get; set; } = [];

    public Task<List<Cliente>> GetAllByIdsAsync(List<int> ids) =>
        Task.FromResult(Clientes.Where(c => ids.Contains(c.Id)).ToList());

    public Task<int?> GetIdByEmailAsync(string email) =>
        Task.FromResult(Clientes.FirstOrDefault(c => c.Email == email)?.Id);

    public Task<Cliente?> GetByIdAsync(int id) => Task.FromResult(Clientes.FirstOrDefault(c => c.Id == id));
    public Task<Cliente?> GetByEmailAsync(string email) => Task.FromResult(Clientes.FirstOrDefault(c => c.Email == email));

    public Task<List<Cliente>> GetAllAsync(FiltroClienteDTO filtro) => throw new NotImplementedException();
    public Task AddAsync(Cliente cliente) => throw new NotImplementedException();
    public Task UpdateAsync(Cliente cliente) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(int id) => throw new NotImplementedException();
    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

public class FakeContratoRepository : IContratoRepository {
    public List<ContratoAutentique> Contratos { get; set; } = [];

    public Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos) =>
        Task.FromResult(Contratos.Where(c => c.Ativo && idPedidos.Contains(c.IdPedido)).ToList());

    public Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) =>
        Task.FromResult(Contratos.FirstOrDefault(c => c.IdPedido == idPedido && c.Ativo));

    public Task SaveAsync(ContratoAutentique contrato, bool commit = true) => throw new NotImplementedException();
    public Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId) => throw new NotImplementedException();
    public Task InativarContratosPorPedidoIdAsync(int idPedido) => throw new NotImplementedException();
    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

public class FakeContratoQueue : IContratoQueue {
    public List<int> Enfileirados { get; } = [];
    public void Enfileirar(int idPedido, int tentativas = 0, bool inativarExistente = false) => Enfileirados.Add(idPedido);
    public ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class FakeNotificationService : INotificationService {
    public List<int> Notificados { get; } = [];
    public Task EnviarNotificacaoNovoPedidoAsync(Pedido pedido) {
        Notificados.Add(pedido.Id);
        return Task.CompletedTask;
    }
}

public class FakeUserManager : UserManager<Usuario> {
    private readonly Usuario? _user;
    private readonly bool _isAdmin;

    public FakeUserManager(Usuario? user, bool isAdmin = false)
        : base(new FakeUserStore(), null!, null!, null!, null!, null!, null!, null!, null!) {
        _user = user;
        _isAdmin = isAdmin;
    }

    public override Task<Usuario?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(_user);
    public override Task<bool> IsInRoleAsync(Usuario user, string role) => Task.FromResult(_isAdmin);
}

public class FakeUserStore : IUserStore<Usuario> {
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

/// <summary>Fábricas compartilhadas para montar entidades de domínio nos testes.</summary>
public static class PedidoTestFactory {
    public static Cliente Cliente(int id = 1, string email = "cliente@teste.com") =>
        new() { Id = id, Nome = "Cliente Teste", Email = email, Telefone = "11999999999", Endereco = "Rua Teste" };

    public static JogoCopia Copia(int idCopia, int idJogo, int idCategoria, StatusJogo status = StatusJogo.Disponivel) =>
        new() {
            Id = idCopia,
            IdJogo = idJogo,
            Status = status,
            Jogo = new Jogo { Id = idJogo, Nome = "Jogo Teste", IdCategoria = idCategoria }
        };

    /// <summary>Cria um pedido Pendente com um item já adicionado.</summary>
    public static Pedido PedidoPendenteComItem(Cliente cliente, JogoCopia copia, int idPeriodo, decimal valor, int qtdeDias, int idItem = 1, int idPedido = 0) {
        var pedido = new Pedido(cliente) { Id = idPedido };
        var item = new ItemPedido {
            Id = idItem,
            IdPeriodo = idPeriodo,
            Valor = valor,
            JogoCopia = copia,
            DataDevolucao = pedido.CalcularDataDevolucao(qtdeDias)
        };
        pedido.AdicionarItem(item);
        return pedido;
    }
}
