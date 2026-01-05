using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum StatusJogo : short {
    Disponivel,
    Reservado,
    Alugado,
    Indisponivel,
    Desativado//jogos/copias excluidas
}

[Table("JOGO_COPIA")]
public class JogoCopia {
    [Key, Column("ID")]
    public int Id { get; set; }
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }
    [Column("STATUS")]
    public StatusJogo Status { get; set; }

    [ForeignKey(nameof(IdJogo))]
    public Jogo? Jogo { get; set; } = null!;
}