using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.FaixaPreco;

public class CadastroFaixaPreco(IFaixaPrecoRepository repository) : FaixaPrecoUseCaseBasico(repository)
{
    public async Task<int> ExecuteAsync(FaixaPrecoDTO faixaPrecoDto)
    {
        var faixaPreco = new Infrastructure.Models.FaixaPreco
        {
            QuantidadeDias = faixaPrecoDto.QuantidadeDias,
            Valor = faixaPrecoDto.Valor
        };

        await _repository.AddAsync(faixaPreco);
        return faixaPreco.Id;
    }
}