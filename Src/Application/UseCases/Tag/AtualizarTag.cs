using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarTag(ITagRepository _repository, ILogger<AtualizarTag> logger) : UseCaseBasico {
    public async Task<bool> ExecuteAsync(TagDTO tagDto) {
        if (string.IsNullOrWhiteSpace(tagDto.Nome)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "O nome da tag é obrigatório."));
            return false;
        }

        logger.LogInformation("Iniciando atualização da tag ID {TagId} ({Nome}).", tagDto.Id, tagDto.Nome);
        var tag = await _repository.GetByIdAsync(tagDto.Id.GetValueOrDefault());
        if (tag == null) {
            logger.LogWarning("Falha ao atualizar: Tag ID {TagId} não encontrada.", tagDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Tag de id {tagDto.Id} não encontrada."));
            return false;
        }

        var filtro = new FiltroTagDTO {
            Nome = tagDto.Nome
        };

        var tagsExistentes = await _repository.GetAllAsync(filtro);
        if (tagsExistentes.Any(c => c.Id != tagDto.Id)) {
            logger.LogWarning("Falha ao atualizar tag {TagId}: Já existe outra tag com o nome {Nome}.", tagDto.Id, tagDto.Nome);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma tag com o mesmo nome."));
        }

        if (!IsValid)
            return false;

        try {
            tagDto.UpdateModel(tag);
            await _repository.UpdateAsync(tag);
            logger.LogInformation("Tag ID {TagId} atualizada com sucesso.", tag.Id);
            return IsValid;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar atualização da tag ID {TagId}.", tagDto.Id);
            throw;
        }
    }
}
