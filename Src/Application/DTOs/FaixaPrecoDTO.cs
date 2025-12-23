using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FaixaPrecoDTO
{
    public int Id { get; set; }
    public int QuantidadeDias { get; set; }
    public decimal Valor { get; set; }

    public static FaixaPrecoDTO FromModel(FaixaPreco faixaPreco)
    {
        return new FaixaPrecoDTO
        {
            Id = faixaPreco.Id,
            QuantidadeDias = faixaPreco.QuantidadeDias,
            Valor = faixaPreco.Valor
        };
    }
}