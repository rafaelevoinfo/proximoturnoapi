using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Renova itens de um pedido entregue num pedido novo, ligado ao original. Só renova o que já
/// está com o cliente: o DTO referencia itens do pedido original, então não há como incluir jogo.
/// O cliente escolhe o período (devolução = hoje + dias); data de devolução manual é só do admin.
/// </summary>
public class RenovarPedido(IPedidoRepository pedidoRepository,
                           ICategoriaPeriodoCache _categoriaPeriodoCache,
                           ValidarCupom _validarCupom,
                           IContratoQueue _contratoQueue,
                           ILogger<RenovarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public const string MensagemAtraso = "Este pedido está em atraso. Entre em contato com a loja para renovar.";
    public const string MensagemDataSoAdmin = "Somente a loja pode informar a data de devolução.";

    public async Task ExecuteAsync(SolicitantePedido solicitante, int idPedido, RenovarPedidoDTO dto) {
        logger.LogInformation("Iniciando renovação para o pedido {PedidoId} com {ItemCount} itens.", idPedido, dto.Itens.Count);
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            logger.LogWarning("Falha na renovação: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (!PodeAlterar(solicitante, pedidoExistente)) {
            logger.LogWarning("Renovação negada: o pedido {PedidoId} não pertence ao cliente {ClienteId}.", idPedido, solicitante.IdCliente);
            return;
        }

        if (pedidoExistente.Status != StatusPedido.Entregue) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Para renovar, o pedido precisa estar entregue."));
            return;
        }

        // Cliente com jogo atrasado precisa falar com a loja; o admin decide se renova.
        if (!solicitante.Admin && pedidoExistente.EstaAtrasado(DateTime.Now)) {
            logger.LogWarning("Renovação negada: o pedido {PedidoId} está em atraso e o solicitante não é admin.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, MensagemAtraso));
            return;
        }

        if (dto.Itens.Count == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum item foi informado para ser renovado"));
            return;
        }

        if (!solicitante.Admin && (dto.DataDevolucao.HasValue || dto.Itens.Any(i => i.DataDevolucao.HasValue))) {
            logger.LogWarning("Renovação negada: data de devolução informada por quem não é admin no pedido {PedidoId}.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, MensagemDataSoAdmin));
            return;
        }

        var itensNovoPedido = ResolverItens(pedidoExistente, dto);

        if (string.IsNullOrWhiteSpace(dto.MetodoPagamento)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Informe a forma de pagamento."));
        }

        if (!IsValid) {
            logger.LogWarning("Renovação do pedido {PedidoId} rejeitada: {Erros}", idPedido, AggregateErrors());
            return;
        }

        ValidacaoCupomResultadoDTO? cupom = null;
        if (!string.IsNullOrWhiteSpace(dto.CupomCodigo)) {
            // Regras normais do cupom: sem IdPedido, a renovação é um pedido novo e conta como uso.
            cupom = await _validarCupom.ExecuteAsync(new ValidarCupomDTO {
                Codigo = dto.CupomCodigo,
                IdCliente = pedidoExistente.Cliente.Id,
                Itens = itensNovoPedido.Select(i => new ItemCupomValidacaoDTO {
                    IdJogo = i.idJogo,
                    IdPeriodo = i.periodo.IdPeriodo
                }).ToList()
            });

            if (!cupom.Valido || cupom.IdCupom is null) {
                logger.LogWarning("Falha ao aplicar cupom na renovação do pedido {PedidoId}: {Mensagem}", idPedido, cupom.Mensagem);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                    string.IsNullOrWhiteSpace(cupom.Mensagem) ? "Cupom inválido." : cupom.Mensagem));
                return;
            }
        }

        var novoPedido = pedidoExistente.Renovar(
            itensNovoPedido.Select(i => ((int idItem, CategoriaPeriodoInfo periodo, DateTime? dataDevolucao)?)(i.idItem, i.periodo, i.dataDevolucao)).ToList(),
            _categoriaPeriodoCache);
        if (novoPedido is null || !pedidoExistente.IsValid || !novoPedido.IsValid) {
            var notifications = pedidoExistente.Notifications
                .Concat(novoPedido?.Notifications ?? [])
                .Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message))
                .ToList();
            logger.LogWarning("Regra de negócio impediu renovação do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", notifications.Select(n => n.Message)));
            AddNotifications((IList<UseCaseNotification>)notifications);
            return;
        }

        // Os jogos já estão com o cliente: não há forma de entrega nem taxa.
        novoPedido.DefinirMetodos(dto.MetodoPagamento, null);
        if (cupom is not null) {
            novoPedido.AplicarCupom(cupom.IdCupom!.Value, cupom.ValorDescontoCalculado);
        }

        try {
            // commit: false só anexa o original; o segundo SaveAsync grava os dois de uma vez.
            await _pedidoRepository.SaveAsync(pedidoExistente, false);
            await _pedidoRepository.SaveAsync(novoPedido);
            logger.LogInformation("Renovação do pedido {PedidoId} concluída. Novo pedido gerado: {NovoPedidoId}.", idPedido, novoPedido.Id);

            _contratoQueue.Enfileirar(novoPedido.Id);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar renovação do pedido {PedidoId}.", idPedido);
            throw;
        }
    }

    /// <summary>
    /// Confere cada item pedido e resolve período e data efetiva. Acumula as notificações, para a
    /// tela mostrar todos os problemas de uma vez.
    /// </summary>
    private List<(int idItem, int idJogo, CategoriaPeriodoInfo periodo, DateTime? dataDevolucao)> ResolverItens(Pedido pedido, RenovarPedidoDTO dto) {
        var hoje = DateTime.Now.Date;
        if (dto.DataDevolucao.HasValue && dto.DataDevolucao.Value.Date <= hoje) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A data de devolução informada deve ser superior à data atual."));
        }

        if (dto.Itens.GroupBy(i => i.Id).Any(g => g.Count() > 1)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O mesmo item foi informado mais de uma vez."));
        }

        var resolvidos = new List<(int, int, CategoriaPeriodoInfo, DateTime?)>();
        foreach (var itemRenovar in dto.Itens.DistinctBy(i => i.Id)) {
            var itemPedido = pedido.Items.FirstOrDefault(i => i.Id == itemRenovar.Id);
            if (itemPedido is null || itemPedido.Status != StatusPedido.Entregue) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                    $"O item {itemRenovar.Id} não pode ser renovado: não pertence a este pedido ou não está com o cliente."));
                continue;
            }

            var nomeJogo = itemPedido.JogoCopia.Jogo?.Nome ?? $"item {itemPedido.Id}";
            var idPeriodo = itemRenovar.IdPeriodo ?? itemPedido.IdPeriodo;
            if (!_categoriaPeriodoCache.TryGetPeriodo(idPeriodo, out var periodo) || periodo is null
                || periodo.IdCategoria != itemPedido.JogoCopia.Jogo?.IdCategoria) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"O período escolhido não é válido para {nomeJogo}."));
                continue;
            }

            if (itemRenovar.DataDevolucao.HasValue && itemRenovar.DataDevolucao.Value.Date <= hoje) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                    $"A data de devolução de {nomeJogo} deve ser superior à data atual."));
                continue;
            }

            resolvidos.Add((itemPedido.Id, itemPedido.JogoCopia.IdJogo, periodo, itemRenovar.DataDevolucao ?? dto.DataDevolucao));
        }

        return resolvidos;
    }
}
