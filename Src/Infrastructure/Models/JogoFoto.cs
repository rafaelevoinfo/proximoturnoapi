using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("JOGO_FOTO")]
public class JogoFoto : BaseModel {
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }

    [Column("URL"), MaxLength(255)]
    public string Url { get; set; } = null!;

    [Column("ORDEM")]
    public int Ordem { get; set; }
}
