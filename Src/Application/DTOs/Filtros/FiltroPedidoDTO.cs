using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroPedidoDTO {
    // Para não-admin, é sobrescrito pelo use case com o cliente do usuário logado.
    // Admin pode filtrar por um cliente específico via ?id_cliente=.
    [FromQuery(Name = "id_cliente")]
    public int? IdCliente { get; set; }
    [FromQuery(Name = "data_inicial")]
    public DateOnly? DataInicial { get; set; }
    [FromQuery(Name = "data_final")]
    public DateOnly? DataFinal { get; set; }
    [FromQuery(Name = "status")]
    public StatusPedido? Status { get; set; }
    [FromQuery(Name = "atrasados")]
    public bool Atrasados { get; set; }
}