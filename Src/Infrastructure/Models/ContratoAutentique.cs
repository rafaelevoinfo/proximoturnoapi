using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum StatusContrato : short {
    Pendente = 0,
    Assinado = 1,
    Rejeitado = 2
}

[Table("CONTRATO_AUTENTIQUE")]
public class ContratoAutentique : BaseModel {
    [Column("ID_PEDIDO")]
    public int IdPedido { get; set; }
    public Domain.Pedido Pedido { get; set; } = null!;

    [Column("AUTENTIQUE_DOCUMENT_ID"), MaxLength(100)]
    public required string AutentiqueDocumentId { get; set; }

    [Column("AUTENTIQUE_PUBLIC_ID"), MaxLength(100)]
    public required string AutentiquePublicId { get; set; }

    [Column("LINK_ASSINATURA"), MaxLength(500)]
    public required string LinkAssinatura { get; set; }

    [Column("STATUS")]
    public StatusContrato Status { get; set; } = StatusContrato.Pendente;

    [Column("DATA_CRIACAO")]
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    [Column("DATA_ASSINATURA")]
    public DateTime? DataAssinatura { get; set; }

    [Column("ATIVO")]
    public bool Ativo { get; set; } = true;
}
