using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Avisa cada cliente dos jogos que ele precisa devolver hoje: uma notificação por cliente,
/// com todos os jogos do dia, mesmo que venham de pedidos diferentes.
/// </summary>
public class EnviarLembretesDevolucao(
    IPedidoRepository _pedidoRepository,
    INotificationService _notificationService,
    ILogger<EnviarLembretesDevolucao> logger) {

    /// <returns>Quantos clientes foram avisados.</returns>
    public async Task<int> ExecuteAsync(DateTime agora) {
        var hoje = agora.Date;
        var pedidos = await _pedidoRepository.ObterComLembreteDevolucaoPendenteAsync(hoje);

        var porCliente = pedidos
            .SelectMany(p => p.Items
                .Where(i => i.Status == StatusPedido.Entregue
                         && i.DataDevolucao.Date == hoje
                         && (i.DataLembreteDevolucao is null || i.DataLembreteDevolucao.Value.Date < hoje))
                .Select(i => (p.Cliente, Item: i)))
            .GroupBy(x => x.Cliente.Id);

        var avisados = 0;
        foreach (var grupo in porCliente) {
            var cliente = grupo.First().Cliente;
            var itens = grupo.Select(x => x.Item).ToList();
            try {
                await _notificationService.EnviarLembreteDevolucaoAsync(cliente, itens);
                // Marca mesmo sem aparelho inscrito: o lembrete do dia é um só, e um aparelho
                // inscrito depois das 10:00 não faz o cliente receber de novo.
                await _pedidoRepository.MarcarLembreteDevolucaoAsync(itens.Select(i => i.Id).ToList(), agora);
                avisados++;
            } catch (Exception ex) {
                logger.LogError(ex, "Erro ao enviar o lembrete de devolução ao cliente {ClienteId}.", cliente.Id);
            }
        }

        logger.LogInformation("Lembretes de devolução de {Dia:dd/MM/yyyy}: {Avisados} cliente(s) avisado(s).", hoje, avisados);
        return avisados;
    }
}
