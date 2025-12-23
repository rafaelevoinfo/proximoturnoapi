using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("TAG")]
public class Tag {
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public string Nome { get; set => field = value.ToLowerInvariant(); } = null!;

    public List<Jogo>? Jogos { get; set; }
}