
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CLIENTE")]
public class Cliente {
    [Column("ID")]
    public int Id { get; set; }
    [Column("NOME"), MaxLength(100)]
    public required string Nome {
        get; set => field = value.ToLowerInvariant();
    }

    [Column("TELEFONE"), MaxLength(15)]
    public required string Telefone { get; set; }

    [Column("ENDERECO"), MaxLength(400)]
    public required string Endereco { get; set; }

    [Column("EMAIL"), MaxLength(100)]
    public required string Email { get; set => field = value.ToLowerInvariant(); }

    [Column("DATA_NASCIMENTO")]
    public DateOnly? DataNascimento { get; set; }

    [Column("COMO_NOS_CONHECEU"), MaxLength(50)]
    public string? ComoNosConheceu { get; set; }

    [Column("ACEITA_RECEBER_OFERTAS")]
    public bool AceitaReceberOfertas { get; set; }
}

