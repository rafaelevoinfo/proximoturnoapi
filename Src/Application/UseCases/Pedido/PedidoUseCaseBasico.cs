using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;

    protected async Task<(JogoCopia copia, CategoriaPeriodoInfo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, ICategoriaPeriodoCache cache) {
        var copias = await jogoRepository.GetAllCopiasByIdJogoAsync(item.IdJogo);
        var copia = copias?.FirstOrDefault(c => c.Status == StatusJogo.Disponivel);

        if (copia is null) {
            var jogoNaoDisp = await jogoRepository.GetResumoByIdAsync(item.IdJogo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não há cópias disponíveis do jogo \"{jogoNaoDisp?.Nome ?? "desconhecido"}\""));
            return null;
        }

        // Descobre a categoria do jogo (carrega o resumo se necessário)
        var idCategoriaJogo = copia.Jogo?.IdCategoria;
        if (idCategoriaJogo is null) {
            var jogo = await jogoRepository.GetResumoByIdAsync(item.IdJogo);
            idCategoriaJogo = jogo?.IdCategoria;
        }

        if (idCategoriaJogo is null || idCategoriaJogo == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Categoria do jogo não foi encontrada"));
            return null;
        }

        if (!cache.TryGetPeriodo(item.IdPeriodo, out var periodo) || periodo!.IdCategoria != idCategoriaJogo) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A categoria deste jogo não permite o período informado."));
            return null;
        }

        if (!IsValid)
            return null;

        return (copia, periodo);
    }
}
