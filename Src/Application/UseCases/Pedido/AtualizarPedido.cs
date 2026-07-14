using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarPedido(IPedidoRepository pedidoRepository,
    IJogoRepository _jogoRepository,
    ICategoriaPeriodoCache _categoriaPeriodoCache,
    ValidarCupom _validarCupom,
    IContratoQueue _contratoQueue,
    ILogger<AtualizarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(NovoPedidoDTO novoPedidoDto) {
        logger.LogInformation("Iniciando atualização do pedido {PedidoId}.", novoPedidoDto.Id);
        var pedido = await _pedidoRepository.GetByIdAsync(novoPedidoDto.Id.GetValueOrDefault());
        if (pedido is null) {
            logger.LogWarning("Falha ao atualizar: Pedido {PedidoId} não encontrado.", novoPedidoDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        pedido.RemoverCupom();
        pedido.DefinirMetodos(novoPedidoDto.MetodoPagamento, novoPedidoDto.MetodoEntrega);

        for (var i = pedido.Items.Count - 1; i >= 0; i--) {
            var itemExistente = pedido.Items[i];
            if (!novoPedidoDto.Items.Any(novoItem => novoItem.Id == itemExistente.Id)) {
                if (!pedido.RemoverItem(itemExistente.Id)) {
                    logger.LogWarning("Falha ao remover item {ItemId} do pedido {PedidoId}: {Errors}", itemExistente.Id, pedido.Id, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                    AddNotifications((IList<UseCaseNotification>)pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                    return;
                }
            }
        }
        foreach (var item in novoPedidoDto.Items!) {
            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaPeriodoCache);
            if (!IsValid) {
                logger.LogWarning("Falha na validação do item (Jogo ID {JogoId}) durante atualização do pedido {PedidoId}.", item.IdJogo, pedido.Id);
                return;
            }

            var dataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias);
            var itemExistente = pedido.Items.FirstOrDefault(pi => pi.Id == item.Id);
            if (itemExistente != null) {
                if (itemExistente.IdJogoCopia == item.IdCopiaJogo &&
                    itemExistente.Valor == resultValidacao.Value.periodo.Valor &&
                    itemExistente.DataDevolucao == dataDevolucao) {
                    continue;
                }
                pedido.RemoverItem(itemExistente);
            }
            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia,
                IdPeriodo = resultValidacao.Value.periodo.IdPeriodo,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)
            };

            if (!pedido.AdicionarItem(itemPedido)) {
                logger.LogWarning("Regra de negócio impediu adição de item ao pedido {PedidoId} durante atualização: {Errors}", pedido.Id, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                var notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications((IList<UseCaseNotification>)notifications);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(novoPedidoDto.CupomCodigo))
        {
            var validacao = await _validarCupom.ExecuteAsync(new ValidarCupomDTO
            {
                Codigo = novoPedidoDto.CupomCodigo,
                IdCliente = pedido.Cliente.Id,
                IdPedido = pedido.Id,
                Itens = novoPedidoDto.Items.Select(i => new ItemCupomValidacaoDTO
                {
                    IdJogo = i.IdJogo,
                    IdPeriodo = i.IdPeriodo
                }).ToList()
            });

            if (!validacao.Valido)
            {
                logger.LogWarning("Falha ao aplicar cupom no pedido {PedidoId}: {Mensagem}", pedido.Id, validacao.Mensagem);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, validacao.Mensagem ?? "Cupom inválido."));
                return;
            }

            if (validacao.IdCupom.HasValue)
            {
                pedido.AplicarCupom(validacao.IdCupom.Value, validacao.ValorDescontoCalculado);

                if (!pedido.IsValid)
                {
                    logger.LogWarning("Falha ao aplicar regras de negócio do cupom no pedido {PedidoId}: {Errors}", pedido.Id, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                    IReadOnlyCollection<UseCaseNotification> notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                    AddNotifications(notifications);
                    return;
                }
            }
            else
            {
                logger.LogWarning("Validação do cupom retornou sucesso mas ID do cupom está ausente para o pedido {PedidoId}.", pedido.Id);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Erro ao associar o cupom."));
                return;
            }
        }

        try {
            await _pedidoRepository.SaveAsync(pedido);
            logger.LogInformation("Pedido {PedidoId} atualizado com sucesso.", pedido.Id);
            
            // Enfileira a nova geração de contrato inativando os anteriores
            _contratoQueue.Enfileirar(pedido.Id, inativarExistente: true);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao atualizar o pedido {PedidoId} no banco de dados.", pedido.Id);
            throw;
        }
    }
}