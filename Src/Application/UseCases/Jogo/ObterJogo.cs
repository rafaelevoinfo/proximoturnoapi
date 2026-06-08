using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterJogo(IJogoRepository _jogoRepository,
                       ITagRepository _tagRepository,
                       DatabaseContext _dbContext,
                       ILogger<ObterJogo> _logger) : JogoUseCaseBasico(_jogoRepository, _tagRepository) {
    public async Task<JogoDTO?> ObterJogoAsync(int id) {
        _logger.LogInformation("Iniciando obtenção do jogo com id {JogoId}.", id);

        var jogo = await _jogoRepository.GetByIdAsync(id);
        if (jogo == null) {
            _logger.LogWarning("Jogo com id {JogoId} não encontrado.", id);
            return null;
        }

        var jogoDto = JogoDTO.FromModel(jogo);
        var mediaAvaliacoes = await _dbContext.Comentarios
            .Where(c => c.IdJogo == id &&
                        c.Status == Domain.StatusComentario.Aprovado)
            .AverageAsync(c => (double?)c.Nota) ?? 0.0;

        jogoDto.MediaAvaliacoes = (short?)Math.Round(mediaAvaliacoes);
        jogoDto.MediaAvaliacoes ??= 0;
        if (jogoDto.MediaAvaliacoes.HasValue && (jogoDto.MediaAvaliacoes.Value > 5 || jogoDto.MediaAvaliacoes.Value < 0)) {
            if (jogoDto.MediaAvaliacoes.Value > 5) {
                jogoDto.MediaAvaliacoes = 5;
            } else {
                jogoDto.MediaAvaliacoes = 0;
            }
        }
        return jogoDto;
    }
}