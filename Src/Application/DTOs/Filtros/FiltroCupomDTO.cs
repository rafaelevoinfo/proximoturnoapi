using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs.Filtros;

public class FiltroCupomDTO
{
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    [FromQuery(Name = "apenas_ativos")]
    public bool? ApenasAtivos { get; set; }
}
