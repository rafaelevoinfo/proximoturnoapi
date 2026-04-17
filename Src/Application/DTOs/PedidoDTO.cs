using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Domain;
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
                Jogo = JogoResumoDTO.FromModel(i.JogoCopia.Jogo!),
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
    public List<NovoItemPedidoDTO> Items { get; set; } = [];
}


public record NovoItemPedidoDTO {
    public int? Id { get; set; }
    [Required]
    public int IdJogo { get; set; }
    public int? IdCopiaJogo { get; set; }
    [Required]
    public int IdPeriodo { get; set; }
}




public record ItemPedidoRenovarDTO {
    [Required]
    public int Id { get; set; }
    [Required]
    public int IdPeriodo { get; set; }
}

public record ItemPedidoDTO {
    public int Id { get; set; }

    public JogoResumoDTO? Jogo { get; set; } = null!;

    public decimal Valor { get; set; }
    public DateTime DataDevolucao { get; set; }

    public bool Renovado { get; set; }
}