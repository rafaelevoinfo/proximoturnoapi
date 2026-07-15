using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroClienteDTO {
    [FromQuery(Name = "nome")]
    public string? Nome { get; set; }
    [FromQuery(Name = "email")]
    public string? Email { get; set; }
    [FromQuery(Name = "telefone")]
    public string? Telefone { get; set; }
    [FromQuery(Name = "cpf")]
    public string? Cpf { get; set; }
    /// <summary>true = apenas ativos, false = apenas inativos, null = ambos.</summary>
    [FromQuery(Name = "ativo")]
    public bool? Ativo { get; set; }
    /// <summary>true = apenas login ativo, false = apenas login inativo, null = ambos. Aplicado no controller.</summary>
    [FromQuery(Name = "loginAtivo")]
    public bool? LoginAtivo { get; set; }
    [FromQuery(Name = "page")]
    public int? Page { get; set; }
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; set; }
}