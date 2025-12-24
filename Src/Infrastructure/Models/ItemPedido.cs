using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;


[Table("PEDIDO_JOGO")]
public class ItemPedido {
    [Column("ID")]
    public int Id { get; set; }
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }
    public Jogo Jogo { get; set; } = null!;

    [Column("VALOR")]
    public decimal Valor { get; set; }

    [Column("DATA_DEVOLUCAO")]
    public DateTime DataDevolucao { get; set; }

    [Column("RENOVADO")]
    public bool Renovado { get; set; }
}
