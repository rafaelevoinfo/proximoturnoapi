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
}
