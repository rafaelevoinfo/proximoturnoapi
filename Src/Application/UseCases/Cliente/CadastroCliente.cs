using System.Transactions;
using Flunt.Notifications;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCliente(IClienteRepository repository, UserManager<Usuario> _userManager) : UseCaseBasico {
    private readonly IClienteRepository _repository = repository;

    public async Task<int> ExecuteAsync(ClienteDTO clienteDto) {
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
            return 0;

        var cliente = clienteDto.ToModel();
        await _repository.StartTransactionAsync();
        try {
            await _repository.AddAsync(cliente);
            var usuario = new Usuario() {
                UserName = cliente.Email,
                Email = cliente.Email,
                Nome = cliente.Nome
            };

            var result = await _userManager.CreateAsync(usuario, clienteDto.Senha);
            if (!result.Succeeded) {
                foreach (var error in result.Errors) {
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, error.Description));
                }
                await _repository.RollbackTransactionAsync();
                return 0;
            }

            var roleResult = await _userManager.AddToRoleAsync(usuario, Roles.Member);
            if (!roleResult.Succeeded) {
                foreach (var error in roleResult.Errors) {
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, error.Description));
                }
                await _repository.RollbackTransactionAsync();
                return 0;
            }

            await _repository.CommitTransactionAsync();
        } catch {
            await _repository.RollbackTransactionAsync();
            throw;
        }
        return cliente.Id;
    }
}