using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroPedidoDTO {
    [FromQuery(Name = "id_cliente")]
    public int? IdCliente { get; set; }
    [FromQuery(Name = "data_inicial")]
    public DateTime? DataInicial { get; set; }
    [FromQuery(Name = "data_final")]
    public DateTime? DataFinal { get; set; }
    [FromQuery(Name = "status")]
    public StatusPedido? Status { get; set; }
    [FromQuery(Name = "atrasados")]
    public bool Atrasados { get; set; }
}