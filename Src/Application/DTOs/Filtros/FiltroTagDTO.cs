using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs.Filtros;

public class FiltroTagDTO
{
    [FromQuery(Name = "nome")]
    public string? Nome { get; set; }
}
