using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases;

public class JogoUseCaseBasico : UseCaseBasico {
    protected readonly IJogoRepository _jogoRepository;
    protected readonly ITagRepository _tagRepository;
    public JogoUseCaseBasico(IJogoRepository jogoRepository, ITagRepository tagRepository) {
        _jogoRepository = jogoRepository;
        _tagRepository = tagRepository;
    }

    protected async Task AtualizarTags(Jogo jogo, ILogger _logger) {
        if (jogo.Tags is not null) {
            for (var i = jogo.Tags.Count - 1; i >= 0; i--) {
                var tag = jogo.Tags[i]!;
                if (tag.Id != 0) {
                    var tagExistente = await _tagRepository.GetByIdAsync(tag.Id);
                    if (tagExistente is null) {
                        tag.Id = 0;
                        _logger.LogWarning($"Tag de id {tag.Id} não encontrada. Criando nova tag com o nome {tag.Nome}.");
                    } else {
                        jogo.Tags.RemoveAt(i);
                        tagExistente.Nome = tag.Nome;
                        jogo.Tags.Add(tagExistente);
                    }
                }
                if (tag.Id == 0) {
                    var tagExistente = await _tagRepository.GetByNomeAsync(tag.Nome);
                    if (tagExistente is not null) {
                        jogo.Tags.RemoveAt(i);
                        jogo.Tags.Add(tagExistente);
                    }
                }
            }
        }
    }
}