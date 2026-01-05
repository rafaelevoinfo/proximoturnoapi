using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.FaixaPreco;

public class FaixaPrecoUseCaseBasico(IPeriodoRepository repository) : UseCaseBasico {
    protected readonly IPeriodoRepository _repository = repository;
}