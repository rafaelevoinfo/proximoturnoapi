using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum TipoLink : short {
    Video,
    Regra
}

[Table("JOGO_LINK")]
public class JogoLink : BaseModel {
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }

    [Column("TITULO"), MaxLength(50)]
    public required string Titulo { get; set; }

    [Column("URL"), MaxLength(300)]
    public required string Url { get; set; }

    [Column("TIPO")]
    public TipoLink Tipo { get; set; }

}