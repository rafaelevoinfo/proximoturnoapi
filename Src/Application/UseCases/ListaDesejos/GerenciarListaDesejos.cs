using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class GerenciarListaDesejos(
    IListaDesejosRepository _repository, 
    IClienteRepository _clienteRepository, 
    UserManager<Usuario> _userManager,
    ILogger<GerenciarListaDesejos> _logger
) : UseCaseBasico {

    public async Task<List<ItemListaDesejos>> GetWishlistAsync(ClaimsPrincipal userClaim) {
        var idCliente = await GetIdCliente(userClaim);
        if (idCliente == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Nenhum cliente vinculado ao usuário logado foi encontrado."));
            return [];
        }
        return await _repository.GetByClienteAsync(idCliente);
    }

    public async Task<bool> AddToWishlistAsync(ClaimsPrincipal userClaim, int idJogo) {
        var idCliente = await GetIdCliente(userClaim);
        if (idCliente == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Nenhum cliente vinculado ao usuário logado foi encontrado."));
            _logger.LogWarning("Tentativa de adicionar à lista de desejos falhou: Cliente não encontrado para o usuário.");
            return false;
        }

        if (await _repository.IsInWishlistAsync(idCliente, idJogo)) return true;

        await _repository.AddAsync(new ItemListaDesejos {
            IdCliente = idCliente,
            IdJogo = idJogo
        });
        
        _logger.LogInformation("Jogo {IdJogo} adicionado à lista de desejos do cliente {IdCliente}.", idJogo, idCliente);
        return true;
    }

    public async Task<bool> RemoveFromWishlistAsync(ClaimsPrincipal userClaim, int idJogo) {
        var idCliente = await GetIdCliente(userClaim);
        if (idCliente == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Nenhum cliente vinculado ao usuário logado foi encontrado."));
            return false;
        }

        await _repository.RemoveAsync(idCliente, idJogo);
        _logger.LogInformation("Jogo {IdJogo} removido da lista de desejos do cliente {IdCliente}.", idJogo, idCliente);
        return true;
    }

    public async Task<bool> IsInWishlistAsync(ClaimsPrincipal userClaim, int idJogo) {
        var idCliente = await GetIdCliente(userClaim);
        if (idCliente == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Nenhum cliente vinculado ao usuário logado foi encontrado."));
            return false;
        }
        return await _repository.IsInWishlistAsync(idCliente, idJogo);
    }

    private async Task<int> GetIdCliente(ClaimsPrincipal userClaim) {
        var user = await _userManager.GetUserAsync(userClaim);
        if (user == null || string.IsNullOrEmpty(user.Email)) return 0;
        
        var idCliente = await _clienteRepository.GetIdByEmailAsync(user.Email);
        return idCliente ?? 0;
    }
}
