using System;
using System.Collections.Generic;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoTests
{
    private Cliente CriarClienteTeste()
    {
        return new Cliente
        {
            Id = 1,
            Nome = "Cliente Teste",
            Email = "teste@teste.com",
            Telefone = "123456",
            Endereco = "Rua Teste"
        };
    }

    private JogoCopia CriarJogoCopiaTeste(decimal valor)
    {
        return new JogoCopia
        {
            Id = 1,
            Status = StatusJogo.Disponivel,
            Jogo = new Jogo
            {
                Id = 10,
                Nome = "Jogo Teste",
                IdCategoria = 1
            }
        };
    }

    [Fact]
    public void CalcularTotal_WhenMetodoEntregaIsEntrega_ShouldIncludeShippingFee()
    {
        // Arrange
        var cliente = CriarClienteTeste();
        var pedido = new Pedido(cliente, "dinheiro", "entrega");
        var item = new ItemPedido
        {
            Id = 1,
            JogoCopia = CriarJogoCopiaTeste(50.0m),
            IdPeriodo = 1,
            Valor = 50.0m
        };

        // Act
        pedido.AdicionarItem(item);

        // Assert
        Assert.Equal(58.0m, pedido.ValorTotal); // 50.0 + 8.0 shipping
    }

    [Fact]
    public void CalcularTotal_WhenMetodoEntregaIsRetirada_ShouldNotIncludeShippingFee()
    {
        // Arrange
        var cliente = CriarClienteTeste();
        var pedido = new Pedido(cliente, "dinheiro", "retirada");
        var item = new ItemPedido
        {
            Id = 1,
            JogoCopia = CriarJogoCopiaTeste(50.0m),
            IdPeriodo = 1,
            Valor = 50.0m
        };

        // Act
        pedido.AdicionarItem(item);

        // Assert
        Assert.Equal(50.0m, pedido.ValorTotal); // 50.0 (no shipping)
    }

    [Fact]
    public void DefinirMetodos_WhenChangingToEntrega_ShouldRecalculateTotalIncludingShippingFee()
    {
        // Arrange
        var cliente = CriarClienteTeste();
        var pedido = new Pedido(cliente, "dinheiro", "retirada");
        var item = new ItemPedido
        {
            Id = 1,
            JogoCopia = CriarJogoCopiaTeste(50.0m),
            IdPeriodo = 1,
            Valor = 50.0m
        };
        pedido.AdicionarItem(item);
        Assert.Equal(50.0m, pedido.ValorTotal);

        // Act
        pedido.DefinirMetodos("dinheiro", "entrega");

        // Assert
        Assert.Equal(58.0m, pedido.ValorTotal); // Updated to include shipping
    }

    [Fact]
    public void DefinirMetodos_WhenChangingToRetirada_ShouldRecalculateTotalExcludingShippingFee()
    {
        // Arrange
        var cliente = CriarClienteTeste();
        var pedido = new Pedido(cliente, "dinheiro", "entrega");
        var item = new ItemPedido
        {
            Id = 1,
            JogoCopia = CriarJogoCopiaTeste(50.0m),
            IdPeriodo = 1,
            Valor = 50.0m
        };
        pedido.AdicionarItem(item);
        Assert.Equal(58.0m, pedido.ValorTotal);

        // Act
        pedido.DefinirMetodos("dinheiro", "retirada");

        // Assert
        Assert.Equal(50.0m, pedido.ValorTotal); // Updated to exclude shipping
    }

    [Fact]
    public void AdicionarItem_DefineItemComoPendente()
    {
        var pedido = new Pedido(CriarClienteTeste());
        var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };

        pedido.AdicionarItem(item);

        Assert.Equal(StatusPedido.Pendente, item.Status);
        Assert.Equal(StatusPedido.Pendente, pedido.Status);
    }

    [Fact]
    public void Entregar_DefineTodosOsItensComoEntregue()
    {
        var pedido = new Pedido(CriarClienteTeste());
        var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };
        pedido.AdicionarItem(item);

        pedido.Entregar(new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));

        Assert.Equal(StatusPedido.Entregue, item.Status);
        Assert.Equal(StatusPedido.Entregue, pedido.Status);
    }

    [Fact]
    public void Cancelar_DefineTodosOsItensComoCancelado()
    {
        var pedido = new Pedido(CriarClienteTeste());
        var item = new ItemPedido { Id = 1, JogoCopia = CriarJogoCopiaTeste(50m), IdPeriodo = 1, Valor = 50m };
        pedido.AdicionarItem(item);

        pedido.Cancelar();

        Assert.Equal(StatusPedido.Cancelado, item.Status);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
    }

    private Pedido PedidoEntregueComDoisItens(out ItemPedido item1, out ItemPedido item2)
    {
        var pedido = new Pedido(CriarClienteTeste());
        item1 = new ItemPedido { Id = 1, JogoCopia = new JogoCopia { Id = 1, IdJogo = 10, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 10, Nome = "J1", IdCategoria = 1 } }, IdPeriodo = 1, Valor = 50m };
        item2 = new ItemPedido { Id = 2, JogoCopia = new JogoCopia { Id = 2, IdJogo = 11, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 11, Nome = "J2", IdCategoria = 1 } }, IdPeriodo = 1, Valor = 50m };
        pedido.AdicionarItem(item1);
        pedido.AdicionarItem(item2);
        pedido.Entregar(new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));
        return pedido;
    }

    [Fact]
    public void Devolver_Parcial_MantemPedidoEntregueEItemNaoDevolvidoEntregue()
    {
        var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);

        var ok = pedido.Devolver(new List<int> { item1.Id });

        Assert.True(ok);
        Assert.Equal(StatusPedido.Devolvido, item1.Status);
        Assert.Equal(StatusJogo.Disponivel, item1.JogoCopia.Status);
        Assert.Equal(StatusPedido.Entregue, item2.Status);
        Assert.Equal(StatusPedido.Entregue, pedido.Status);
    }

    [Fact]
    public void Devolver_Todos_DeixaPedidoDevolvido()
    {
        var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);

        var ok = pedido.Devolver(null);

        Assert.True(ok);
        Assert.Equal(StatusPedido.Devolvido, item1.Status);
        Assert.Equal(StatusPedido.Devolvido, item2.Status);
        Assert.Equal(StatusPedido.Devolvido, pedido.Status);
    }

    [Fact]
    public void Renovar_Parcial_MantemPedidoAntigoEntregueEGeraNovoSoComRenovado()
    {
        var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);
        var cache = new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
        var periodo = new ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo(1, 7, 50m, 1, "categoria");
        var itensRenovar = new List<(int idItem, ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo periodo)?> { (item1.Id, periodo) };

        var novo = pedido.Renovar(itensRenovar, cache);

        Assert.NotNull(novo);
        Assert.Equal(StatusPedido.Devolvido, item1.Status);
        Assert.Equal(StatusPedido.Entregue, item2.Status);
        Assert.Equal(StatusPedido.Entregue, pedido.Status);        // sobrou item fora
        Assert.Single(novo!.Items);
        Assert.Equal(StatusPedido.Entregue, novo.Status);
    }

    [Fact]
    public void Renovar_Todos_DeixaPedidoAntigoDevolvido()
    {
        var pedido = PedidoEntregueComDoisItens(out var item1, out var item2);
        var cache = new ProximoTurnoApi.Tests.Fakes.FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
        var periodo = new ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo(1, 7, 50m, 1, "categoria");
        var itensRenovar = new List<(int idItem, ProximoTurnoApi.Infrastructure.Services.CategoriaPeriodoInfo periodo)?> { (item1.Id, periodo), (item2.Id, periodo) };

        var novo = pedido.Renovar(itensRenovar, cache);

        Assert.NotNull(novo);
        Assert.Equal(StatusPedido.Devolvido, pedido.Status);
        Assert.Equal(2, novo!.Items.Count);
    }
}
