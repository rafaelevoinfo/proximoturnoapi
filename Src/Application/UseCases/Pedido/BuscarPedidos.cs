using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class BuscarPedidos(
    IPedidoRepository pedidoRepository,
    IClienteRepository _clienteRepository,
    IContratoRepository _contratoRepository,
    UserManager<Usuario> _userManager,
    ILogger<BuscarPedidos> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task<List<PedidoDTO>> ExecuteAsync(ClaimsPrincipal user, FiltroPedidoDTO filtro) {
        logger.LogInformation("Buscando lista de pedidos com filtros: {@Filtro}", filtro);
        if (!await AdicionarFiltroPorCliente(user, _clienteRepository, filtro)) {
            logger.LogWarning("Falha na busca de pedidos: Usuário logado não possui um cliente vinculado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não encontrado."));
            return [];
        }

        var pedidosModels = await _pedidoRepository.GetAllAsync(filtro);
        var pedidos = pedidosModels.Select(PedidoDTO.FromModel).ToList();

        if (pedidos.Count > 0) {
            var pedidoIds = pedidos.Select(p => p.Id).ToList();
            var contratos = await _contratoRepository.GetActiveByPedidoIdsAsync(pedidoIds);
            var contratosDict = contratos.ToDictionary(c => c.IdPedido);

            foreach (var p in pedidos) {
                if (contratosDict.TryGetValue(p.Id, out var contrato)) {
                    p.ContratoStatus = contrato.Status.ToString();
                    p.ContratoLink = contrato.LinkAssinatura;
                }
            }
        }

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

        var dto = PedidoDTO.FromModel(pedido);
        var contrato = await _contratoRepository.GetByPedidoIdAsync(idPedido);
        if (contrato is not null) {
            dto.ContratoStatus = contrato.Status.ToString();
            dto.ContratoLink = contrato.LinkAssinatura;
        }

        return dto;
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