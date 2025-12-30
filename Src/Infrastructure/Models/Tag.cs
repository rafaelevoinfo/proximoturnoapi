using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("TAG")]
public class Tag {
    private string _nome = null!;
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public string Nome { get => _nome; set => _nome = value.ToLowerInvariant(); }

    public List<Jogo>? Jogos { get; set; }
}