using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Jogo;

public class AtualizarJogo(IJogoRepository repository) : UseCaseBasico {
    private readonly IJogoRepository _repository = repository;

    public async Task<bool> ExecuteAsync(JogoDTO jogoDto) {
        var jogo = await _repository.GetByIdAsync(jogoDto.Id);
        if (jogo is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Jogo de id {jogoDto.Id} não encontrado."));
            return false;
        }

        var jogosExistentes = await _repository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Any(j => j.Id != jogoDto.Id)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um jogo com o mesmo nome."));
        }

        if (!IsValid)
            return false;

        jogoDto.UpdateModel(jogo);
        await _repository.UpdateAsync(jogo);
        return IsValid;
    }
}
