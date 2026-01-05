using System.Collections.Immutable;
using System.Security.Claims;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Sprache;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroPedido(IPedidoRepository pedidoRepository,
                            IJogoRepository _jogoRepository,
                            IClienteRepository _clienteRepository,
                            IPeriodoRepository _periodoRepository,
                            ICategoriaRepository _categoriaRepository) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task<int> ExecuteAsync(ClaimsPrincipal user, NovoPedidoDTO novoPedidoDto) {
        var cliente = await _clienteRepository.GetByEmailAsync(user.Identity?.Name ?? "");//BuscarIdClienteLogado(user.Identity?.Name ?? "");
        if (cliente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
            return 0;
        }

        var pedido = new Pedido(cliente);
        foreach (var item in novoPedidoDto.Items) {
            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _periodoRepository, _categoriaRepository);
            if (!IsValid) {
                return 0;
            }

            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia!,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.DataHora.AddDays(resultValidacao.Value.periodo.QuantidadeDias)
            };
            if (!pedido.AdicionarItem(itemPedido)) {
                var notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications((IList<UseCaseNotification>)notifications);
                return 0;
            }
        }

        await _pedidoRepository.SaveAsync(pedido);
        return pedido.Id;
    }

    private async Task<int?> BuscarIdClienteLogado(string email) {
        return await _clienteRepository.GetIdByEmailAsync(email);
    }
}