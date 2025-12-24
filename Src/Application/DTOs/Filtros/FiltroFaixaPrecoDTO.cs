using Microsoft.AspNetCore.Mvc;

namespace ProximoTurnoApi.Application.DTOs.Filtros;

public class FiltroFaixaPrecoDTO {
    [FromQuery(Name = "quantidade_dias")]
    public int? QuantidadeDias { get; set; }
    [FromQuery(Name = "valor")]
    public decimal? Valor { get; set; }
}