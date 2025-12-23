using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum PedidoJogoStatus : short {
    Alugado,
    Devolvido,
    Renovado
}

public static class PedidoJogoStatusExtensions {
    public static JogoStatus ToJogoStatus(this PedidoJogoStatus status) {
        return status switch {
            PedidoJogoStatus.Alugado => JogoStatus.Alugado,
            PedidoJogoStatus.Devolvido => JogoStatus.Disponivel,
            PedidoJogoStatus.Renovado => JogoStatus.Alugado,
            _ => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
        };
    }
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
