using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

[Table("LINK")]
public class Link {
    [Column("ID")]
    public int Id { get; set; }
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }

    [Column("TITULO"), MaxLength(50)]
    public required string Titulo { get; set; }

    [Column("URL"), MaxLength(300)]
    public required string Url { get; set; }

}