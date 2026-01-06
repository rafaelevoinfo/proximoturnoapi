using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;

    protected async Task<(JogoCopia copia, CategoriaPeriodo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, ICategoriaRepository categoriaRepository) {
        var copia = await jogoRepository.GetCopiaByIdAsync(item.IdCopiaJogo);
        if (copia is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Jogo não disponível"));
        }

        var categoria = await categoriaRepository.GetByIdAsync(copia?.Jogo?.IdCategoria ?? 0);
        CategoriaPeriodo? periodo = null;
        if (categoria is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Categoria do jogo não foi encontrada"));
        } else {
            periodo = categoria.Periodos.FirstOrDefault(f => f.Id == item?.IdPeriodo);
            if (periodo is null) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A categoria deste jogo não permite o período informado."));
            }
        }
        if (!IsValid)
            return null;

        return (copia!, periodo!);
    }
}