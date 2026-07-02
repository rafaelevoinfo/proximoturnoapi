using Flunt.Notifications;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusCliente(IClienteRepository _repository, ILogger<AtualizarStatusCliente> logger) : UseCaseBasico {
    public async Task<bool> ExecuteAsync(int id, bool ativo) {
        logger.LogInformation("Iniciando alteração de status do cliente ID {ClienteId} para Ativo = {Ativo}.", id, ativo);
        var cliente = await _repository.GetByIdAsync(id);
        if (cliente == null) {
            logger.LogWarning("Falha ao alterar status: Cliente ID {ClienteId} não encontrado.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Cliente de id {id} não encontrado."));
            return false;
        }

        try {
            cliente.Ativo = ativo;
            await _repository.UpdateAsync(cliente);
            logger.LogInformation("Cliente ID {ClienteId} atualizado com status Ativo = {Ativo} com sucesso.", id, ativo);
            return true;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao alterar status do cliente ID {ClienteId}.", id);
            throw;
        }
    }
}
