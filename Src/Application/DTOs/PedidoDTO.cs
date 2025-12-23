using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record PedidoDTO {
    public int Id { get; set; }

    public ClienteDTO Cliente { get; set; } = null!;

    public DateTime DataHora { get; set; }

    public decimal ValorTotal { get; set; }

    public List<ItemPedidoDTO> Items { get; set; } = [];
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
    public int IdJogo { get; set; }
    public decimal Valor { get; set; }

    public DateTime DataDevolucao { get; set; }

    public PedidoJogoStatus Status { get; set; }

}