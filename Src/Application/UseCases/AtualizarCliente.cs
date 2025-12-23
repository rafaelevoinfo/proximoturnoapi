using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarCliente(IClienteRepository repository) : UseCaseBasico {
    private readonly IClienteRepository _repository = repository;

    public async Task<bool> ExecuteAsync(ClienteDTO clienteDto) {
        var cliente = await _repository.GetByIdAsync(clienteDto.Id.GetValueOrDefault());
        if (cliente == null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Cliente de id {clienteDto.Id} não encontrado."));
            return false;
        }

        var filtro = new FiltroClienteDTO {
            Email = clienteDto.Email
        };

        var clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Any(c => c.Id != clienteDto.Id)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo email."));
        }

        filtro = new FiltroClienteDTO {
            Telefone = clienteDto.Telefone
        };

        clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Any(c => c.Id != clienteDto.Id)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo telefone."));
        }

        if (!IsValid)
            return false;

        clienteDto.ToModel(cliente);
        await _repository.UpdateAsync(cliente);
        return IsValid;
    }
}