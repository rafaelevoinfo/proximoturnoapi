using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroJogo(IJogoRepository _jogoRepository,
                          ITagRepository _tagRepository,
                          ILogger<CadastroJogo> _logger) : JogoUseCaseBasico(_jogoRepository, _tagRepository) {

    public async Task<int> ExecuteAsync(JogoDTO jogoDto) {
        var jogosExistentes = await _jogoRepository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Já existe um jogo com o mesmo nome."));
        }

        if (!IsValid)
            return 0;

        var jogo = jogoDto.ToModel();
        await AtualizarTags(jogo, _logger);
        await _jogoRepository.SaveAsync(jogo, false);
        await _jogoRepository.SaveAsync(new JogoCopia() {
            IdJogo = jogo.Id,
        });

        return jogo.Id;
    }

    public async Task<int?> AdicionarCopia(int idJogo) {
        if (!await _jogoRepository.ExisteAsync(idJogo)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Jogo inexistente"));
            return null;
        }
        var copia = new JogoCopia() {
            IdJogo = idJogo,
            Status = StatusJogo.Disponivel
        };
        await _jogoRepository.SaveAsync(copia);
        return copia.Id;
    }
}
