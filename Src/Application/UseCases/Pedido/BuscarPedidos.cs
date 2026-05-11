using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class BuscarPedidos(IPedidoRepository pedidoRepository, IClienteRepository _clienteRepository, UserManager<Usuario> _userManager, ILogger<BuscarPedidos> logger) : PedidoUseCaseBasico(pedidoRepository) {
    public async Task<List<PedidoDTO>> ExecuteAsync(ClaimsPrincipal user, FiltroPedidoDTO filtro) {
        logger.LogInformation("Buscando lista de pedidos com filtros: {@Filtro}", filtro);
        if (!await AdicionarFiltroPorCliente(user, _clienteRepository, filtro)) {
            logger.LogWarning("Falha na busca de pedidos: Usuário logado não possui um cliente vinculado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não encontrado."));
            return [];
        }

        var pedidos = (await _pedidoRepository.GetAllAsync(filtro))
                .Select(PedidoDTO.FromModel)
                .ToList();

        logger.LogInformation("{Count} pedidos encontrados para os critérios informados.", pedidos.Count);
        return pedidos;
    }

    public async Task<PedidoDTO?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
        logger.LogInformation("Buscando detalhes do pedido {PedidoId}.", idPedido);
        var pedido = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedido == null) {
            logger.LogWarning("Pedido {PedidoId} não encontrado.", idPedido);
            return null;
        }

        if (!userClaim.IsInRole(Roles.Admin)) {
            var user = await _userManager.GetUserAsync(userClaim);
            var idCliente = await _clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente is null || idCliente.Value != pedido.Cliente?.Id) {
                logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou acessar o pedido {PedidoId} de outro cliente.", user?.Email, idPedido);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Nenhum cliente vinculado ao usuário logado foi encontrado"));
                return null;
            }
        }

        return PedidoDTO.FromModel(pedido);
    }

    private async Task<bool> AdicionarFiltroPorCliente(ClaimsPrincipal userClaim, IClienteRepository clienteRepository, FiltroPedidoDTO filtro) {
        if (!userClaim.IsInRole(Roles.Admin)) {
            var user = await _userManager.GetUserAsync(userClaim);
            var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente.GetValueOrDefault() == 0) {
                return false;
            }
            filtro.IdCliente = idCliente.Value;
        }
        return true;

    }
}