using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("LINK")]
public class Link : BaseModel {
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }

    [Column("TITULO"), MaxLength(50)]
    public required string Titulo { get; set; }

    [Column("URL"), MaxLength(300)]
    public required string Url { get; set; }

}