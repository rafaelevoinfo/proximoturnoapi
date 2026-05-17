using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Application.DTOs.Filtros;
using Org.BouncyCastle.Tls.Crypto;
using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Application.UseCases;

public class JogoUseCaseBasico : UseCaseBasico {
    protected readonly IJogoRepository _jogoRepository;
    protected readonly ITagRepository _tagRepository;
    protected List<Tag> _tagsCache = new();
    public JogoUseCaseBasico(IJogoRepository jogoRepository, ITagRepository tagRepository) {
        _jogoRepository = jogoRepository;
        _tagRepository = tagRepository;
    }

    protected async Task ValidarTags(List<TagDTO>? tags, ILogger _logger) {
        if (_tagsCache.Count == 0) {
            _logger.LogInformation("Carregando cache de tags para validação.");
            _tagsCache = await _tagRepository.GetAllAsync(true);
        }

        if (tags is not null && tags.Count > 0) {
            var tagsForaDoCache = tags.Where(tag => !_tagsCache.Any(cache => cache.Id == tag.Id)).ToList();
            if (tagsForaDoCache.Count > 0) {
                _logger.LogWarning("Tags fornecidas para validação não encontradas no cache: {Tags}.", string.Join(", ", tagsForaDoCache.Select(t => t.Nome)));
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"As seguintes tags são inválidas: {string.Join(", ", tagsForaDoCache.Select(t => t.Nome))}"));
            }
        }
    }

    protected async Task ValidarLinks(List<JogoLinkDTO>? links, ILogger _logger) {
        if (links is not null && links.Count > 0) {
            foreach (var link in links) {
                if (!Uri.IsWellFormedUriString(link.Url, UriKind.Absolute)) {
                    _logger.LogWarning("Link fornecido para validação é inválido: {Url}.", link.Url);
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"O link '{link.Url}' é inválido."));
                }
            }
        }
    }

    protected async Task ValidarFotos(List<JogoFotoDTO>? fotos, ILogger _logger) {
        if (fotos is not null && fotos.Count > 0) {
            foreach (var foto in fotos) {
                if (!Uri.IsWellFormedUriString(foto.Url, UriKind.Absolute)) {
                    _logger.LogWarning("URL de foto fornecida para validação é inválida: {Url}.", foto.Url);
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"A URL da foto '{foto.Url}' é inválida."));
                }
            }
        }
    }

    protected async Task AtualizarTags(Jogo jogo, List<TagDTO>? tagsDto) {
        if (tagsDto is null || tagsDto.Count == 0) {
            jogo.Tags?.Clear();
            return;
        }

        jogo.Tags ??= [];
        for (var i = jogo.Tags.Count - 1; i >= 0; i--) {
            var tag = jogo.Tags[i];
            if (!tagsDto.Any(t => t.Id == tag.Id)) {
                jogo.Tags.RemoveAt(i);
            }
        }

        //Aqui o validarTags ja garantiu que são tags validas, entao nao preciso validar novamente, apenas adicionar as novas
        foreach (var tagDto in tagsDto) {
            if (!jogo.Tags.Any(t => t.Id == tagDto.Id)) {
                var tag = _tagsCache.FirstOrDefault(t => t.Id == tagDto.Id);
                if (tag is not null) {
                    jogo.Tags.Add(tag);
                }
            }
        }
    }


}