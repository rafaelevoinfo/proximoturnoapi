using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Pedido do admin para refazer a indexação dos manuais de um jogo do zero: extração do PDF,
/// revisão, chunks e vetores. Serve para quando a extração saiu errada ou incompleta; o
/// trabalho em si roda na fila, em segundo plano.
/// </summary>
public class ReindexarManuais(ILogger<ReindexarManuais> _logger,
                              IIndexacaoManualRepository _repository,
                              IManualQueue _queue) : UseCaseBasico {

    /// <summary>Devolve quantos manuais foram para a fila; zero com notificação quando não há nenhum.</summary>
    public async Task<int> ExecuteAsync(int idJogo) {
        var ids = await _repository.GetIdsLinksRegraAsync(idJogo);
        if (ids.Count == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                                                       "Este jogo não tem manual de regras para indexar."));
            return 0;
        }

        foreach (var id in ids) {
            _queue.Enfileirar(new ManualJob(id, idJogo, Forcar: true));
        }

        _logger.LogInformation("Reindexação forçada de {Quantidade} manual(is) do jogo {IdJogo} enfileirada.", ids.Count, idJogo);
        return ids.Count;
    }
}
