using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarJogo(IJogoRepository jogoRepository, ITagRepository tagRepository, ILogger<AtualizarJogo> _logger) : JogoUseCaseBasico(jogoRepository, tagRepository) {

    public async Task<bool> ExecuteAsync(JogoDTO jogoDto) {
        var jogo = await _jogoRepository.GetByIdAsync(jogoDto.Id);
        if (jogo is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Jogo de id {jogoDto.Id} não encontrado."));
            return false;
        }

        var jogosExistentes = await _jogoRepository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Any(j => j.Id != jogoDto.Id)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um jogo com o mesmo nome."));
        }

        if (!IsValid)
            return false;

        jogoDto.UpdateModel(jogo);
        await AtualizarTags(jogo, _logger);
        await _jogoRepository.SaveAsync(jogo);
        return IsValid;
    }
}
