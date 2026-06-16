using System;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record ContratoDTO {
    public int Id { get; init; }
    public int IdPedido { get; init; }
    public string LinkAssinatura { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime DataCriacao { get; init; }
    public DateTime? DataAssinatura { get; init; }

    public static ContratoDTO FromModel(ContratoAutentique contrato) {
        return new ContratoDTO {
            Id = contrato.Id,
            IdPedido = contrato.IdPedido,
            LinkAssinatura = contrato.LinkAssinatura,
            Status = contrato.Status.ToString(),
            DataCriacao = contrato.DataCriacao,
            DataAssinatura = contrato.DataAssinatura
        };
    }
}
