using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Domain;

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

    [Column("STATUS")]
    public StatusPedido Status { get; set; }

    /// <summary>
    /// Quando o cliente recebeu o lembrete de devolução deste item. Evita repetir o aviso no
    /// mesmo dia se a API reiniciar; uma data de devolução alterada depois volta a ser avisada.
    /// </summary>
    [Column("DATA_LEMBRETE_DEVOLUCAO")]
    public DateTime? DataLembreteDevolucao { get; set; }
}
