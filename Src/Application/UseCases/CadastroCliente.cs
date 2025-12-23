using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCliente(IClienteRepository repository) : UseCaseBasico {
    private readonly IClienteRepository _repository = repository;

    public async Task<bool> ExecuteAsync(ClienteDTO clienteDto) {
        var filtro = new FiltroClienteDTO {
            Email = clienteDto.Email
        };

        var clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo email."));
        }

        filtro = new FiltroClienteDTO {
            Telefone = clienteDto.Telefone
        };

        clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo telefone."));
        }

        if (!IsValid)
            return false;

        var cliente = clienteDto.ToModel();
        await _repository.AddAsync(cliente);
        return IsValid;
    }
}