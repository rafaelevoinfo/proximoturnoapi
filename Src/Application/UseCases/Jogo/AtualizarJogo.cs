using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarJogo(IJogoRepository _jogoRepository, ITagRepository _tagRepository, ILogger<AtualizarJogo> _logger) : JogoUseCaseBasico(_jogoRepository, _tagRepository) {

    public async Task<bool> ExecuteAsync(JogoDTO jogoDto) {
        _logger.LogInformation("Iniciando atualização do jogo ID {JogoId} ({Nome}).", jogoDto.Id, jogoDto.Nome);
        var jogo = await _jogoRepository.GetByIdAsync(jogoDto.Id);

        if (jogo is null) {
            _logger.LogWarning("Falha ao atualizar: Jogo ID {JogoId} não encontrado.", jogoDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Jogo de id {jogoDto.Id} não encontrado."));
            return false;
        }

        var nome = jogoDto.Nome?.Trim();
        var jogosExistentes = await _jogoRepository.GetAllAsync(new FiltroJogoDTO { Nome = nome });
        if (jogosExistentes.Any(j => j.Id != jogoDto.Id && string.Equals(j.Nome?.Trim(), nome, StringComparison.OrdinalIgnoreCase))) {
            _logger.LogWarning("Falha ao atualizar jogo {JogoId}: Já existe outro jogo com o nome {Nome}.", jogoDto.Id, jogoDto.Nome);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um jogo com o mesmo nome."));
        }

        await ValidarTags(jogoDto.Tags, _logger);
        await ValidarLinks(jogoDto.Links, _logger);
        await ValidarFotos(jogoDto.Fotos, _logger);

        if (!IsValid)
            return false;

        try {
            jogoDto.UpdateModel(jogo);
            await AtualizarTags(jogo, jogoDto.Tags);
            await _jogoRepository.SaveAsync(jogo);
            _logger.LogInformation("Jogo ID {JogoId} atualizado com sucesso.", jogo.Id);
            return IsValid;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro fatal ao salvar atualização do jogo ID {JogoId}.", jogoDto.Id);
            throw;
        }
    }


}
