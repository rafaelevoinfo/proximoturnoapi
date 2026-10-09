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

    public int? IdCupom { get; set; }
    public string? CupomCodigo { get; set; }
    public decimal ValorDesconto { get; set; }

    public StatusPedido Status { get; set; }

    public bool Atrasado { get; set; }

    public string? MetodoPagamento { get; set; }

    public string? MetodoEntrega { get; set; }

    public DateTime? DataHoraAlteracao { get; set; }

    public string? ContratoStatus { get; set; }
    public string? ContratoLink { get; set; }

    [Required]
    public List<ItemPedidoDTO>? Items { get; set; } = [];

    public static PedidoDTO FromModel(Pedido pedido) {
        return new PedidoDTO {
            Id = pedido.Id,
            Cliente = ClienteResumoDTO.FromModel(pedido.Cliente!),
            DataHora = pedido.DataHora,
            ValorTotal = pedido.ValorTotal,
            IdCupom = pedido.IdCupom,
            CupomCodigo = pedido.Cupom?.Codigo,
            ValorDesconto = pedido.ValorDesconto,
            Status = pedido.Status,
            Atrasado = pedido.EstaAtrasado(DateTime.Now),
            MetodoPagamento = pedido.MetodoPagamento,
            MetodoEntrega = pedido.MetodoEntrega,
            DataHoraAlteracao = pedido.DataHoraAlteracao,
            Items = pedido.Items.Select(i => new ItemPedidoDTO {
                Id = i.Id,
                Jogo = JogoResumoDTO.FromModel(i.JogoCopia.Jogo!),
                IdPeriodo = i.IdPeriodo,
                Valor = i.Valor,
                DataDevolucao = i.DataDevolucao,
                Status = i.Status,
                Renovado = pedido.IdPedidoOriginal != null
            }).ToList()
        };
    }
}

public record NovoPedidoDTO {
    public int? Id { get; set; }

    [Required]
    public List<NovoItemPedidoDTO> Items { get; set; } = [];

    public int? IdCliente { get; set; }

    public string? MetodoPagamento { get; set; }

    public string? MetodoEntrega { get; set; }

    public string? CupomCodigo { get; set; }
}


public record NovoItemPedidoDTO {
    public int? Id { get; set; }
    [Required]
    public int IdJogo { get; set; }
    public int? IdCopiaJogo { get; set; }
    [Required]
    public int IdPeriodo { get; set; }
}




public record RenovarPedidoDTO {
    [Required]
    public List<ItemPedidoRenovarDTO> Itens { get; set; } = [];

    /// <summary>Só admin. Vale para os itens sem data própria.</summary>
    public DateTime? DataDevolucao { get; set; }

    public string? CupomCodigo { get; set; }
    public string? MetodoPagamento { get; set; }
}

public record ItemPedidoRenovarDTO {
    /// <summary>Item do pedido original.</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Null mantém o período atual do item.</summary>
    public int? IdPeriodo { get; set; }

    /// <summary>Só admin. Tem preferência sobre a data do pedido.</summary>
    public DateTime? DataDevolucao { get; set; }
}

public record ItemPedidoDTO {
    public int Id { get; set; }

    public JogoResumoDTO? Jogo { get; set; } = null!;

    public int IdPeriodo { get; set; }

    public decimal Valor { get; set; }
    public DateTime DataDevolucao { get; set; }

    public StatusPedido Status { get; set; }

    public bool Renovado { get; set; }
}