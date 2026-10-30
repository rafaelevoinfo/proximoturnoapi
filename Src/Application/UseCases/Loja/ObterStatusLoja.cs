using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterStatusLoja(IConfiguracaoLojaRepository _repository) : UseCaseBasico {
    public async Task<StatusLojaDTO> ExecuteAsync() {
        return StatusLojaDTO.FromModel(await _repository.GetAsync());
    }
}
