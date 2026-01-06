using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("JOGO_LINK")]
public class JogoLink : BaseModel {
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }
    [Column("ID_LINK")]
    public int IdLink { get; set; }
    public Jogo? Jogo { get; set; }
    public Link? Link { get; set; }
}