using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

[Table("PEDIDO")]
public class Pedido {
    [Column("ID")]
    public int Id { get; set; }
    [Column("ID_CLIENTE")]
    public int IdCliente { get; set; }
    public Cliente? Cliente { get; set; }
    [Column("DATA_HORA")]
    public DateTime DataHora { get; set; }
    [Column("VALOR_TOTAL")]
    public decimal ValorTotal { get; set; }
    public List<PedidoJogo> Jogos { get; set; } = [];
}
