using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CATEGORIA")]
public class Categoria : BaseModel {
    private string _descricao = null!;

    [Column("DESCRICAO"), MaxLength(100)]
    public string Descricao { get => _descricao; set => _descricao = value.ToLowerInvariant(); }

    [Column("ATIVO")]
    public bool Ativo { get; set; } = true;

    public List<CategoriaPeriodo> Periodos { get; set; } = [];
}