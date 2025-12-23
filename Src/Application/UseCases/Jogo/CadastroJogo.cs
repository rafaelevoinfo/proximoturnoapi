using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroJogo(IJogoRepository _jogoRepository,
                          ITagRepository _tagRepository,
                          ILogger<CadastroJogo> _logger) : JogoUseCaseBasico(_jogoRepository, _tagRepository) {

    public async Task<int> ExecuteAsync(JogoDTO jogoDto) {
        var jogosExistentes = await _jogoRepository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um jogo com o mesmo nome."));
        }

        if (!IsValid)
            return 0;

        var jogo = jogoDto.ToModel();
        await AtualizarTags(jogo, _logger);
        await _jogoRepository.SaveAsync(jogo);
        return jogo.Id;
    }
}
