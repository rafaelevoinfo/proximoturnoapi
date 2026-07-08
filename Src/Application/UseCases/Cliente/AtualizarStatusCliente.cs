using System.Security.Claims;
using Flunt.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusCliente(IClienteRepository _repository, UserManager<Usuario> _userManager, ILogger<AtualizarStatusCliente> logger) : UseCaseBasico {
    public async Task<bool> ExecuteAsync(int id, bool ativo, ClaimsPrincipal user) {
        logger.LogInformation("Iniciando alteração de status do cliente ID {ClienteId} para Ativo = {Ativo}.", id, ativo);
        var cliente = await _repository.GetByIdAsync(id);
        if (cliente == null) {
            logger.LogWarning("Falha ao alterar status: Cliente ID {ClienteId} não encontrado.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Cliente de id {id} não encontrado."));
            return false;
        }

        var usuarioCliente = await _userManager.FindByEmailAsync(cliente.Email);
        if (!ativo && usuarioCliente != null && await _userManager.IsInRoleAsync(usuarioCliente, Roles.Admin)) {
            logger.LogWarning("Tentativa de inativar o cliente ID {ClienteId} ({Email}), que é um administrador.", id, cliente.Email);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Não é possível inativar um administrador."));
            return false;
        }

        await _repository.StartTransactionAsync();
        try {
            cliente.Ativo = ativo;
            await _repository.UpdateAsync(cliente);

            var usuarioLogado = await _userManager.GetUserAsync(user);
            if (usuarioLogado != null) {
                var isAdmin = await _userManager.IsInRoleAsync(usuarioLogado, Roles.Admin);
                if (!isAdmin && usuarioLogado.Email != cliente.Email) {
                    logger.LogWarning("Usuário logado tentou alterar o status de outro cliente.");
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Você não tem permissão para alterar o status deste cliente."));
                    await _repository.RollbackTransactionAsync();
                    return false;
                }
            }

            // Bloqueia/desbloqueia o login do cliente via o mecanismo nativo de lockout do Identity,
            // já que "Ativo" no Cliente não é consultado pela autenticação.
            // Não usamos DateTimeOffset.MaxValue: seus 7 dígitos de fração de segundo são
            // arredondados para cima pela coluna datetime(6) do MySQL, estourando o ano 9999.
            if (usuarioCliente != null) {
                await _userManager.SetLockoutEnabledAsync(usuarioCliente, true);
                var lockoutEnd = ativo ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddYears(100);
                var lockoutResult = await _userManager.SetLockoutEndDateAsync(usuarioCliente, lockoutEnd);
                if (!lockoutResult.Succeeded) {
                    logger.LogWarning("Falha ao atualizar bloqueio de acesso do cliente {Email}: {Errors}", cliente.Email, string.Join(", ", lockoutResult.Errors.Select(e => e.Description)));
                    foreach (var error in lockoutResult.Errors) {
                        AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, error.Description));
                    }
                    await _repository.RollbackTransactionAsync();
                    return false;
                }
            }

            logger.LogInformation("Cliente ID {ClienteId} atualizado com status Ativo = {Ativo} com sucesso.", id, ativo);

            await _repository.CommitTransactionAsync();
            return true;
        } catch (Exception ex) {
            await _repository.RollbackTransactionAsync();
            logger.LogError(ex, "Erro fatal ao alterar status do cliente ID {ClienteId}.", id);
            throw;
        }
    }
}
