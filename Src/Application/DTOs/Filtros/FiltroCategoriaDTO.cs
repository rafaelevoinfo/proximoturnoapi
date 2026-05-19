using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroCategoriaDTO
{
    [FromQuery(Name = "descricao")]
    public string? Descricao { get; set; }

    [FromQuery(Name = "apenasAtivos")]
    public bool ApenasAtivos { get; set; } = false;
}
