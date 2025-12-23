using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.FaixaPreco;

public class AtualizarFaixaPreco(IFaixaPrecoRepository repository) : FaixaPrecoUseCaseBasico(repository)
{
    public async Task<bool> ExecuteAsync(FaixaPrecoDTO faixaPrecoDto)
    {
        var faixaPreco = await _repository.GetByIdAsync(faixaPrecoDto.Id);

        if (faixaPreco == null)
        {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Faixa de preço não encontrada."));
            return false;
        }

        faixaPreco.QuantidadeDias = faixaPrecoDto.QuantidadeDias;
        faixaPreco.Valor = faixaPrecoDto.Valor;

        await _repository.UpdateAsync(faixaPreco);
        return true;
    }
}