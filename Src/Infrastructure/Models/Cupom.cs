using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("CUPOM")]
public class Cupom : BaseModel
{
    [Required, Column("CODIGO"), MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required, Column("TIPO_DESCONTO")]
    public TipoDesconto TipoDesconto { get; set; }

    [Required, Column("VALOR_DESCONTO", TypeName = "decimal(18,2)")]
    public decimal ValorDesconto { get; set; }

    [Column("DATA_INICIO")]
    public DateTime? DataInicio { get; set; }

    [Column("DATA_FIM")]
    public DateTime? DataFim { get; set; }

    [Column("LIMITE_USO_GLOBAL")]
    public int? LimiteUsoGlobal { get; set; }

    [Column("LIMITE_USO_CLIENTE")]
    public int? LimiteUsoCliente { get; set; }

    [Column("CONDICAO"), MaxLength(500)]
    public string? Condicao { get; set; }

    [Required, Column("ATIVO")]
    public bool Ativo { get; set; } = true;
}
