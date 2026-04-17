using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;

    protected async Task<(JogoCopia copia, CategoriaPeriodo periodo)?> ValidarAdicionarItem(NovoItemPedidoDTO item, IJogoRepository jogoRepository, ICategoriaRepository categoriaRepository) {
        var copias = await jogoRepository.GetAllCopiasByIdJogoAsync(item.IdJogo);
        var copia = copias?.FirstOrDefault(c => c.Status == StatusJogo.Disponivel);

        if (copia is null) {
            var jogo = await jogoRepository.GetByIdAsync(item.IdJogo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não há cópias disponíveis do jogo \"{jogo?.Nome ?? "desconhecido"}\""));
        }

        var categoria = await categoriaRepository.GetByIdAsync(copia?.Jogo?.IdCategoria ?? 0);
        
        // Se a cópia foi encontrada mas não tem o jogo carregado, tenta carregar o jogo para pegar a categoria
        if (copia != null && copia.Jogo == null) {
            var jogo = await jogoRepository.GetByIdAsync(item.IdJogo);
            categoria = await categoriaRepository.GetByIdAsync(jogo?.IdCategoria ?? 0);
        }

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