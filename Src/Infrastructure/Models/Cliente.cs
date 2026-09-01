
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CLIENTE")]
public class Cliente : BaseModel {
    private string _nome = null!;
    private string _email = null!;

    [Column("NOME"), MaxLength(100)]
    public required string Nome {
        get => _nome; set => _nome = value.ToLowerInvariant();
    }

    [Column("TELEFONE"), MaxLength(15)]
    public required string Telefone { get; set; }

    [Column("ENDERECO"), MaxLength(400)]
    public required string Endereco { get; set; }

    [Column("EMAIL"), MaxLength(100)]
    public required string Email { get => _email; set => _email = value.ToLowerInvariant(); }

    /// <summary>Armazenado apenas com os 11 dígitos, sem máscara. Opcional: clientes antigos não possuem.</summary>
    [Column("CPF"), MaxLength(11)]
    public string? Cpf { get; set; }

    [Column("DATA_NASCIMENTO")]
    public DateOnly? DataNascimento { get; set; }

    [Column("COMO_NOS_CONHECEU"), MaxLength(50)]
    public string? ComoNosConheceu { get; set; }

    [Column("ACEITA_RECEBER_OFERTAS")]
    public bool AceitaReceberOfertas { get; set; }

    [Column("ATIVO")]
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Preenchido quando o titular exerce o direito de eliminação (LGPD Art. 18, VI).
    /// Null = conta nunca excluída. Invariante: preenchido implica Ativo == false.
    /// </summary>
    [Column("DATA_ANONIMIZACAO")]
    public DateTime? DataAnonimizacao { get; set; }
}

