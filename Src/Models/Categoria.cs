using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

[Table("CATEGORIA")]
public class Categoria {
    [Column("ID")]
    public int Id { get; set; }

    [Column("DESCRICAO"), MaxLength(100)]
    public required string Descricao { get; set; }
}