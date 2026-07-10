using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarCliente(IClienteRepository _repository, ILogger<AtualizarCliente> logger) : UseCaseBasico {
    public async Task<bool> ExecuteAsync(ClienteDTO clienteDto) {
        logger.LogInformation("Iniciando atualização do cliente ID {ClienteId} ({Email}).", clienteDto.Id, clienteDto.Email);
        var cliente = await _repository.GetByIdAsync(clienteDto.Id.GetValueOrDefault());
        if (cliente == null) {
            logger.LogWarning("Falha ao atualizar: Cliente ID {ClienteId} não encontrado.", clienteDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Cliente de id {clienteDto.Id} não encontrado."));
            return false;
        }
        if (!cliente.Ativo) {
            logger.LogWarning("Falha ao atualizar: Cliente ID {ClienteId} está inativo.", clienteDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Cliente de id {clienteDto.Id} está inativo e não pode ser atualizado."));
            return false;
        }

        var filtro = new FiltroClienteDTO {
            Email = clienteDto.Email
        };

        var clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Any(c => c.Id != clienteDto.Id)) {
            logger.LogWarning("Falha ao atualizar cliente ID {ClienteId}: O email {Email} já está em uso por outro cliente.", clienteDto.Id, clienteDto.Email);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo email."));
        }

        filtro = new FiltroClienteDTO {
            Telefone = clienteDto.Telefone
        };

        clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Any(c => c.Id != clienteDto.Id)) {
            logger.LogWarning("Falha ao atualizar cliente ID {ClienteId}: O telefone {Telefone} já está em uso por outro cliente.", clienteDto.Id, clienteDto.Telefone);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo telefone."));
        }

        // O CPF é opcional, então só é comparado quando informado: sem isso, todos os clientes
        // antigos (que têm CPF nulo) colidiriam entre si.
        if (!string.IsNullOrEmpty(clienteDto.Cpf)) {
            filtro = new FiltroClienteDTO {
                Cpf = clienteDto.Cpf
            };

            clientesExistentes = await _repository.GetAllAsync(filtro);
            if (clientesExistentes.Any(c => c.Id != clienteDto.Id)) {
                logger.LogWarning("Falha ao atualizar cliente ID {ClienteId}: O CPF já está em uso por outro cliente.", clienteDto.Id);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo CPF."));
            }
        }

        if (!IsValid)
            return false;

        try {
            clienteDto.UpdateModel(cliente);
            await _repository.UpdateAsync(cliente);
            logger.LogInformation("Cliente ID {ClienteId} atualizado com sucesso.", cliente.Id);
            return IsValid;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar atualização do cliente ID {ClienteId}.", clienteDto.Id);
            throw;
        }
    }
}