using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

public enum PedidoJogoStatus : short {
    Alugado,
    Devolvido,
    Renovado
}

[Table("PEDIDO_JOGO")]
public class PedidoJogo {
    [Column("ID")]
    public int Id { get; set; }
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }
    public Jogo? Jogo { get; set; }

    [Column("VALOR")]
    public decimal Valor { get; set; }

    [Column("DATA_DEVOLUCAO")]
    public DateTime DataDevolucao { get; set; }

    [Column("STATUS")]
    public PedidoJogoStatus Status { get; set; }
}
