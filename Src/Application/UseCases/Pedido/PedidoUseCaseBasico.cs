using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;

    protected async Task<(JogoCopia copia, Periodo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, IPeriodoRepository periodoRepository, ICategoriaRepository categoriaRepository) {
        var copia = await jogoRepository.GetCopiaByIdAsync(item.IdCopiaJogo);
        if (copia is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Jogo não disponível"));
        }

        var periodo = await periodoRepository.GetByIdAsync(item.IdPeriodo);
        if (periodo is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Período de aluguel não informado"));
        }

        var categoria = await categoriaRepository.GetByIdAsync(copia?.Jogo?.IdCategoria ?? 0);
        if (categoria is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Categoria do jogo não foi encontrada"));
        } else {
            if (!categoria.Periodos.Any(f => f.Id == periodo?.Id)) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A categoria deste jogo não permite o período informado."));
            }
        }
        if (!IsValid)
            return null;

        return (copia!, periodo!);
    }
}