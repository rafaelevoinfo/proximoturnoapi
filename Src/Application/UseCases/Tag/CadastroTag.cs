using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Tag;

public class CadastroTag(ITagRepository repository) : UseCaseBasico
{
    private readonly ITagRepository _repository = repository;

    public async Task<int> ExecuteAsync(TagDTO tagDto)
    {
        var filtro = new FiltroTagDTO
        {
            Nome = tagDto.Nome
        };

        var tagsExistentes = await _repository.GetAllAsync(filtro);
        if (tagsExistentes.Count > 0)
        {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma tag com o mesmo nome."));
        }

        if (!IsValid)
            return 0;

        var tag = tagDto.ToModel();
        await _repository.AddAsync(tag);
        return tag.Id;
    }
}
