using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ValidarCupomTests
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

        public Task<List<Cupom>> GetAllAsync(FiltroCupomDTO filtro) => throw new NotImplementedException();

        public Task<int> GetUsoCountGlobalAsync(int cupomId, int? idPedidoExcluir = null)
        {
            var query = Pedidos.AsQueryable().Where(p => p.IdCupom == cupomId && p.Status != StatusPedido.Cancelado);
            if (idPedidoExcluir.HasValue)
            {
                query = query.Where(p => p.Id != idPedidoExcluir.Value);
            }
            return Task.FromResult(query.Count());
        }

        public Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId, int? idPedidoExcluir = null)
        {
            var query = Pedidos.AsQueryable().Where(p => p.IdCupom == cupomId && p.Cliente.Id == clienteId && p.Status != StatusPedido.Cancelado);
            if (idPedidoExcluir.HasValue)
            {
                query = query.Where(p => p.Id != idPedidoExcluir.Value);
            }
            return Task.FromResult(query.Count());
        }

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

    private class FakeJogoRepository : IJogoRepository
    {
        public Task<List<JogoLink>> GetJogosNaoIndexadosAsync(int? quantidade = null) => throw new NotImplementedException();
        public List<Jogo> Jogos { get; set; } = new();
        public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) => Task.FromResult(Jogos.Where(j => ids.Contains(j.Id)).ToList());

        public Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) => throw new NotImplementedException();
        public Task<List<Jogo>> GetMaisAlugadosAsync() => throw new NotImplementedException();
        public Task<List<Jogo>> GetNovidadesAsync(int quantidade = 3) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo) => throw new NotImplementedException();
        public Task<Jogo?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<Jogo?> GetResumoByIdAsync(int id) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetCopiasAsync(int id) => throw new NotImplementedException();
        public Task<JogoCopia?> GetCopiaByIdAsync(int id) => throw new NotImplementedException();
        public Task SaveAsync(Jogo jogo, bool commit = true) => throw new NotImplementedException();
        public Task SaveAsync(JogoCopia jogo, bool commit = true) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<bool> ExisteAsync(int id) => throw new NotImplementedException();
        public Task<bool> CopiaExisteAndDisponivel(int id) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task StartTransactionAsync() => throw new NotImplementedException();
        public Task CommitTransactionAsync() => throw new NotImplementedException();
        public Task RollbackTransactionAsync() => throw new NotImplementedException();
    }

    private class FakeCategoriaRepository : ICategoriaRepository
    {
        public List<Categoria> Categorias { get; set; } = new();
        public Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro) => Task.FromResult(Categorias);
        public Task<Categoria?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task SaveAsync(Categoria categoria, bool commit = true) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
    }

    private class FakeLogger : ILogger<ValidarCupom>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleUseCoupon_WhenEditingSameOrder_ShouldPass()
    {
        // Arrange
        var cupom = new Cupom
        {
            Id = 1,
            Codigo = "UNICO10",
            Ativo = true,
            LimiteUsoGlobal = 1,
            TipoDesconto = TipoDesconto.Percentual,
            ValorDesconto = 10
        };

        var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var order = new Pedido(cliente) { Id = 100 };
        order.AplicarCupom(cupom.Id, 5.0m);

        var jogo = new Jogo { Id = 10, IdCategoria = 1, Nome = "Jogo Teste" };
        var categoria = new Categoria
        {
            Id = 1,
            Descricao = "Categoria Teste",
            Ativo = true,
            Periodos = new List<CategoriaPeriodo>
            {
                new CategoriaPeriodo { Id = 1, QuantidadeDias = 7, Valor = 50.0m }
            }
        };

        var cupomRepo = new FakeCupomRepository { Cupons = { cupom }, Pedidos = { order } };
        var jogoRepo = new FakeJogoRepository { Jogos = { jogo } };
        var categoriaRepo = new FakeCategoriaRepository { Categorias = { categoria } };
        var useCase = new ValidarCupom(cupomRepo, jogoRepo, categoriaRepo, new FakeLogger());

        var dto = new ValidarCupomDTO
        {
            Codigo = "UNICO10",
            IdCliente = 1,
            IdPedido = 100, // Editing the same order where the coupon was used
            Itens = new List<ItemCupomValidacaoDTO>
            {
                new ItemCupomValidacaoDTO { IdJogo = 10, IdPeriodo = 1 }
            }
        };

        // Act
        var result = await useCase.ExecuteAsync(dto);

        // Assert
        Assert.True(result.Valido);
        Assert.Equal(5.0m, result.ValorDescontoCalculado); // 10% of 50.0m
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleUseCoupon_WhenEditingDifferentOrder_ShouldFail()
    {
        // Arrange
        var cupom = new Cupom
        {
            Id = 1,
            Codigo = "UNICO10",
            Ativo = true,
            LimiteUsoGlobal = 1,
            TipoDesconto = TipoDesconto.Percentual,
            ValorDesconto = 10
        };

        var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "123456", Endereco = "Rua Teste" };
        var order = new Pedido(cliente) { Id = 100 };
        order.AplicarCupom(cupom.Id, 5.0m);

        var jogo = new Jogo { Id = 10, IdCategoria = 1, Nome = "Jogo Teste" };
        var categoria = new Categoria
        {
            Id = 1,
            Descricao = "Categoria Teste",
            Ativo = true,
            Periodos = new List<CategoriaPeriodo>
            {
                new CategoriaPeriodo { Id = 1, QuantidadeDias = 7, Valor = 50.0m }
            }
        };

        var cupomRepo = new FakeCupomRepository { Cupons = { cupom }, Pedidos = { order } };
        var jogoRepo = new FakeJogoRepository { Jogos = { jogo } };
        var categoriaRepo = new FakeCategoriaRepository { Categorias = { categoria } };
        var useCase = new ValidarCupom(cupomRepo, jogoRepo, categoriaRepo, new FakeLogger());

        var dto = new ValidarCupomDTO
        {
            Codigo = "UNICO10",
            IdCliente = 1,
            IdPedido = 200, // Editing a DIFFERENT order (or new order)
            Itens = new List<ItemCupomValidacaoDTO>
            {
                new ItemCupomValidacaoDTO { IdJogo = 10, IdPeriodo = 1 }
            }
        };

        // Act
        var result = await useCase.ExecuteAsync(dto);

        // Assert
        Assert.False(result.Valido);
        Assert.Equal("Cupom inválido.", result.Mensagem);
    }

    [Fact]
    public async Task ExecuteAsync_WithCaseInsensitiveCode_ShouldPass()
    {
        // Arrange
        var cupom = new Cupom
        {
            Id = 1,
            Codigo = "UNICO10",
            Ativo = true,
            LimiteUsoGlobal = 5,
            TipoDesconto = TipoDesconto.Percentual,
            ValorDesconto = 10
        };

        var jogo = new Jogo { Id = 10, IdCategoria = 1, Nome = "Jogo Teste" };
        var categoria = new Categoria
        {
            Id = 1,
            Descricao = "Categoria Teste",
            Ativo = true,
            Periodos = new List<CategoriaPeriodo>
            {
                new CategoriaPeriodo { Id = 1, QuantidadeDias = 7, Valor = 50.0m }
            }
        };

        var cupomRepo = new FakeCupomRepository { Cupons = { cupom } };
        var jogoRepo = new FakeJogoRepository { Jogos = { jogo } };
        var categoriaRepo = new FakeCategoriaRepository { Categorias = { categoria } };
        var useCase = new ValidarCupom(cupomRepo, jogoRepo, categoriaRepo, new FakeLogger());

        var dto = new ValidarCupomDTO
        {
            Codigo = "unico10", // Lowercase input
            IdCliente = 1,
            Itens = new List<ItemCupomValidacaoDTO>
            {
                new ItemCupomValidacaoDTO { IdJogo = 10, IdPeriodo = 1 }
            }
        };

        // Act
        var result = await useCase.ExecuteAsync(dto);

        // Assert
        Assert.True(result.Valido);
    }
}
