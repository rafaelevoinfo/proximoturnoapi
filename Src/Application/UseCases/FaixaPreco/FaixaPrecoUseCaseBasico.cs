using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.FaixaPreco;

public class FaixaPrecoUseCaseBasico(IFaixaPrecoRepository repository) : UseCaseBasico
{
    protected readonly IFaixaPrecoRepository _repository = repository;
}