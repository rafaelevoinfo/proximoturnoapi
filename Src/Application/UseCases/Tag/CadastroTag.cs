using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroTag(ITagRepository _repository, ILogger<CadastroTag> logger) : UseCaseBasico {
    public async Task<int> ExecuteAsync(TagDTO tagDto) {
        if (string.IsNullOrWhiteSpace(tagDto.Nome)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "O nome da tag é obrigatório."));
            return 0;
        }

        logger.LogInformation("Iniciando cadastro de nova tag: {Nome}", tagDto.Nome);
        var filtro = new FiltroTagDTO {
            Nome = tagDto.Nome
        };

        var tagsExistentes = await _repository.GetAllAsync(filtro);
        if (tagsExistentes.Count > 0) {
            logger.LogWarning("Falha ao cadastrar tag: Já existe uma tag com o nome {Nome}.", tagDto.Nome);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma tag com o mesmo nome."));
        }

        if (!IsValid)
            return 0;

        var tag = tagDto.ToModel();
        try {
            await _repository.AddAsync(tag);
            logger.LogInformation("Tag {TagId} ({Nome}) cadastrada com sucesso.", tag.Id, tag.Nome);
            return tag.Id;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar a tag {Nome} no banco de dados.", tagDto.Nome);
            throw;
        }
    }
}
