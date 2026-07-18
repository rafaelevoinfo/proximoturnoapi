using System.Collections.Generic;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoDTOTests {
    [Fact]
    public void FromModel_ExpoeStatusDoItem_EDerivaRenovado() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1);
        var pedido = new Pedido(PedidoTestFactory.Cliente()) { Id = 1 };
        pedido.AdicionarItem(new ItemPedido { Id = 1, IdPeriodo = 1, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(1, 10, 1) });
        pedido.Entregar(cache);

        var dto = PedidoDTO.FromModel(pedido);

        Assert.Equal(StatusPedido.Entregue, dto.Items![0].Status);
        Assert.False(dto.Items[0].Renovado);      // pedido sem PedidoOriginal
    }
}
