using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Manda aos admins um resumo dos pedidos com jogos atrasados: um único push por dia, com
/// todos os pedidos, enquanto continuarem atrasados.
/// </summary>
public class EnviarAlertaPedidosAtrasados(
    IPedidoRepository _pedidoRepository,
    INotificationService _notificationService,
    ILogger<EnviarAlertaPedidosAtrasados> logger) {

    /// <returns>Quantos pedidos entraram no resumo.</returns>
    public async Task<int> ExecuteAsync(DateTime agora) {
        var hoje = agora.Date;
        var pedidos = await _pedidoRepository.ObterComAlertaAtrasoPendenteAsync(hoje);

        // A devolução vence às 23:59:59 do dia, então o atraso começa à meia-noite seguinte.
        var atrasados = pedidos
            .Select(p => p.Items
                .Where(i => i.Status == StatusPedido.Entregue
                         && i.DataDevolucao < hoje
                         && (i.DataAlertaAtraso is null || i.DataAlertaAtraso.Value.Date < hoje))
                .ToList())
            .Zip(pedidos, (itens, pedido) => (pedido, itens))
            .Where(x => x.itens.Count > 0)
            .Select(x => new PedidoAtrasado(x.pedido, x.itens, x.itens.Max(i => (hoje - i.DataDevolucao.Date).Days)))
            .ToList();

        if (atrasados.Count == 0) {
            logger.LogInformation("Nenhum pedido atrasado em {Dia:dd/MM/yyyy}.", hoje);
            return 0;
        }

        await _notificationService.EnviarAlertaPedidosAtrasadosAsync(atrasados);
        // Marca mesmo sem aparelho inscrito: o resumo do dia é um só.
        await _pedidoRepository.MarcarAlertaAtrasoAsync(atrasados.SelectMany(a => a.Itens).Select(i => i.Id).ToList(), agora);

        logger.LogInformation("Resumo de atrasados de {Dia:dd/MM/yyyy} enviado aos admins: {Quantidade} pedido(s).", hoje, atrasados.Count);
        return atrasados.Count;
    }
}
