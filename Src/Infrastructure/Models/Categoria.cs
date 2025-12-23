using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CATEGORIA")]
public class Categoria {
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public required string Descricao { get; set => field = value.ToLowerInvariant(); }

    public ICollection<FaixaPreco> FaixasPreco { get; set; } = new List<FaixaPreco>();
}