using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroJogo(IJogoRepository _jogoRepository,
                          ITagRepository _tagRepository,
                          ILogger<CadastroJogo> _logger) : JogoUseCaseBasico(_jogoRepository, _tagRepository) {

    public async Task<int> ExecuteAsync(JogoDTO jogoDto) {
        _logger.LogInformation("Iniciando cadastro de novo jogo: {Nome}", jogoDto.Nome);
        var jogosExistentes = await _jogoRepository.GetAllAsync(new FiltroJogoDTO { Nome = jogoDto.Nome });
        if (jogosExistentes.Count > 0) {
            _logger.LogWarning("Falha ao cadastrar jogo: Já existe um jogo com o nome {Nome}.", jogoDto.Nome);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Já existe um jogo com o mesmo nome."));
        }

        await ValidarTags(jogoDto.Tags, _logger);
        await ValidarLinks(jogoDto.Links, _logger);
        await ValidarFotos(jogoDto.Fotos, _logger);

        if (!IsValid)
            return 0;

        try {
            var jogo = jogoDto.ToModel();
            await AtualizarTags(jogo, jogoDto.Tags);
            await _jogoRepository.SaveAsync(jogo);
            _logger.LogInformation("Jogo {JogoId} ({Nome}) cadastrado com sucesso com {Qtde} cópias.", jogo.Id, jogo.Nome, jogoDto.Copias?.Count ?? 1);
            return jogo.Id;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro fatal ao salvar o jogo {Nome} no banco de dados.", jogoDto.Nome);
            throw;
        }
    }

    public async Task<int?> AdicionarCopia(int idJogo) {
        _logger.LogInformation("Solicitada adição de cópia para o jogo ID {JogoId}.", idJogo);
        if (!await _jogoRepository.ExisteAsync(idJogo)) {
            _logger.LogWarning("Falha ao adicionar cópia: Jogo ID {JogoId} não encontrado.", idJogo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Jogo inexistente"));
            return null;
        }
        var copia = new JogoCopia() {
            IdJogo = idJogo,
            Status = StatusJogo.Disponivel
        };

        try {
            await _jogoRepository.SaveAsync(copia);
            _logger.LogInformation("Nova cópia (ID {CopiaId}) adicionada com sucesso para o jogo {JogoId}.", copia.Id, idJogo);
            return copia.Id;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro fatal ao salvar nova cópia para o jogo {JogoId}.", idJogo);
            throw;
        }
    }
}
