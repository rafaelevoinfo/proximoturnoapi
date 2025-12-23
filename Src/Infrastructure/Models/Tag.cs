using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

[Table("TAG")]
public class Tag {
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public string Nome { get; set; } = null!;

    public List<Jogo>? Jogos { get; set; }
}