using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Tag;

public class AtualizarTag(ITagRepository repository) : UseCaseBasico
{
    private readonly ITagRepository _repository = repository;

    public async Task<bool> ExecuteAsync(TagDTO tagDto)
    {
        var tag = await _repository.GetByIdAsync(tagDto.Id.GetValueOrDefault());
        if (tag == null)
        {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Tag de id {tagDto.Id} não encontrada."));
            return false;
        }

        var filtro = new FiltroTagDTO
        {
            Nome = tagDto.Nome
        };

        var tagsExistentes = await _repository.GetAllAsync(filtro);
        if (tagsExistentes.Any(c => c.Id != tagDto.Id))
        {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma tag com o mesmo nome."));
        }

        if (!IsValid)
            return false;

        tagDto.UpdateModel(tag);
        await _repository.UpdateAsync(tag);
        return IsValid;
    }
}
