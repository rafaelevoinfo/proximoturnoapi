using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class BuscarPedidos(IPedidoRepository pedidoRepository, IClienteRepository _clienteRepository, UserManager<Usuario> _userManager) : PedidoUseCaseBasico(pedidoRepository) {
    public async Task<List<PedidoDTO>> ExecuteAsync(ClaimsPrincipal user, FiltroPedidoDTO filtro) {
        if (!await AdicionarFiltroPorCliente(user, _clienteRepository, filtro)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não encontrado."));
            return [];
        }

        var pedidos = (await _pedidoRepository.GetAllAsync(filtro))
                .Select(PedidoDTO.FromModel)
                .ToList();

        return pedidos;
    }

    public async Task<PedidoDTO?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
        var pedido = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedido == null) {
            return null;
        }

        if (!userClaim.IsInRole(Roles.Admin)) {
            var user = await _userManager.GetUserAsync(userClaim);
            var cliente = _clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (cliente is null || cliente.Id != pedido.Cliente?.Id) {
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