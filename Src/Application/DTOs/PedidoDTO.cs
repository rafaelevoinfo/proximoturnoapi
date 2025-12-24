using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record PedidoDTO {
    public int Id { get; set; }

    [Required]
    public ClienteResumoDTO? Cliente { get; set; } = null!;

    public DateTime DataHora { get; set; }

    public decimal ValorTotal { get; set; }


    public StatusPedido Status { get; set; }

    [Required]
    public List<ItemPedidoDTO>? Items { get; set; } = [];

    public static PedidoDTO FromModel(Pedido pedido) {
        return new PedidoDTO {
            Id = pedido.Id,
            Cliente = ClienteResumoDTO.FromModel(pedido.Cliente!),
            DataHora = pedido.DataHora,
            ValorTotal = pedido.ValorTotal,
            Status = pedido.Status,
            Items = pedido.Items.Select(i => new ItemPedidoDTO {
                Id = i.Id,
                Jogo = JogoResumoDTO.FromModel(i.Jogo!),
                Valor = i.Valor,
                DataDevolucao = i.DataDevolucao,
                Renovado = i.Renovado
            }).ToList()
        };
    }
}

public record NovoPedidoDTO {
    public int? Id { get; set; }

    [Required]
    public int IdCliente { get; set; }

    [Required]
    public List<ItemPedidoDTO> Items { get; set; } = [];
}

public record ItemPedidoDTO {
    public int Id { get; set; }
    [Required]
    public JogoResumoDTO? Jogo { get; set; } = null!;
    [Required]
    public decimal? Valor { get; set; }
    [Required]
    public DateTime? DataDevolucao { get; set; }

    public bool Renovado { get; set; }
}