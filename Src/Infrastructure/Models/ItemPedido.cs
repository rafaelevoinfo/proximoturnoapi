using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;


[Table("PEDIDO_ITEM")]
public class ItemPedido : BaseModel {
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    [Column("ID_JOGO_COPIA")]
    public int IdJogoCopia { get; set; }
    public JogoCopia JogoCopia { get; set; } = null!;

    [Column("VALOR")]
    public decimal Valor { get; set; }

    [Column("ID_PERIODO")]
    public int IdPeriodo { get; set; }

    [Column("DATA_DEVOLUCAO")]
    public DateTime DataDevolucao { get; set; }

    [Column("RENOVADO")]
    public bool Renovado { get; set; }
}
