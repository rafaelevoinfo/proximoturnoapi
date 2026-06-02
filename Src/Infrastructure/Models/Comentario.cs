using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("COMENTARIO")]
public class Comentario : BaseModel
{
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }

    [Column("ID_JOGO")]
    public int IdJogo { get; set; }

    [Column("ID_CLIENTE")]
    public int IdCliente { get; set; }

    [Column("TEXTO"), MaxLength(1000)]
    public string Texto { get; set; } = null!;

    [Column("NOTA")]
    public int Nota { get; set; }

    [Column("DATA_HORA")]
    public DateTime DataHora { get; set; }

    [ForeignKey(nameof(IdPedido))]
    public Pedido Pedido { get; set; } = null!;

    [ForeignKey(nameof(IdJogo))]
    public Jogo Jogo { get; set; } = null!;

    [ForeignKey(nameof(IdCliente))]
    public Cliente Cliente { get; set; } = null!;
}
