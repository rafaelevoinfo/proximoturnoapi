using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroPedidoDTO {
    //Preenchido automaticamente pelo use case baseado no usuario logado
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