using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("LISTA_DESEJOS")]
public class ItemListaDesejos : BaseModel {
    [Column("ID_CLIENTE")]
    public required int IdCliente { get; set; }

    [Column("ID_JOGO")]
    public required int IdJogo { get; set; }

    public Cliente? Cliente { get; set; }
    public Jogo? Jogo { get; set; }
}
