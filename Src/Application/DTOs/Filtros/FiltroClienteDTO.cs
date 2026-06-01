using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroClienteDTO {
    [FromQuery(Name = "nome")]
    public string? Nome { get; set; }
    [FromQuery(Name = "email")]
    public string? Email { get; set; }
    [FromQuery(Name = "telefone")]
    public string? Telefone { get; set; }
    [FromQuery(Name = "page")]
    public int? Page { get; set; }
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; set; }
}