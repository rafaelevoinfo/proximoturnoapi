using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Jogo;

public class CadastroJogo(IJogoRepository repository) : UseCaseBasico {
    private readonly IJogoRepository _repository = repository;

    public async Task<int> ExecuteAsync(JogoDTO jogoDto) {
        var jogosExistentes = await _repository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um jogo com o mesmo nome."));
        }

        if (!IsValid)
            return 0;

        var jogo = jogoDto.ToModel();
        await _repository.AddAsync(jogo);
        return jogo.Id;
    }
}
