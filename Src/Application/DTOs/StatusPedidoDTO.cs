using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public record StatusPedidoDTO {
    public StatusPedido Status { get; set; }
}