using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CATEGORIA")]
public class Categoria {
    private string _descricao = null!;
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public string Descricao { get => _descricao; set => _descricao = value.ToLowerInvariant(); }

    public ICollection<Periodo> Periodos { get; set; } = [];
}