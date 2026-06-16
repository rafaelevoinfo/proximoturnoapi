using System.Transactions;
using Flunt.Notifications;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCliente(IClienteRepository _repository, UserManager<Usuario> _userManager, ILogger<CadastroCliente> logger) : UseCaseBasico {
    public async Task<int> ExecuteAsync(ClienteDTO clienteDto) {
        logger.LogInformation("Iniciando cadastro de novo cliente: {Nome} ({Email})", clienteDto.Nome, clienteDto.Email);
        var filtro = new FiltroClienteDTO {
            Email = clienteDto.Email
        };

        var clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Count > 0) {
            logger.LogWarning("Falha ao cadastrar cliente: Já existe um cliente com o email {Email}.", clienteDto.Email);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo email."));
        }

        filtro = new FiltroClienteDTO {
            Telefone = clienteDto.Telefone
        };

        clientesExistentes = await _repository.GetAllAsync(filtro);
        if (clientesExistentes.Count > 0) {
            logger.LogWarning("Falha ao cadastrar cliente {Email}: Já existe um cliente com o telefone {Telefone}.", clienteDto.Email, clienteDto.Telefone);
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
                Nome = cliente.Nome,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(usuario, clienteDto.Senha);
            if (!result.Succeeded) {
                logger.LogWarning("Falha ao criar usuário Identity para o cliente {Email}: {Errors}", cliente.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                foreach (var error in result.Errors) {
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, error.Description));
                }
                await _repository.RollbackTransactionAsync();
                return 0;
            }

            var roleResult = await _userManager.AddToRoleAsync(usuario, Roles.Member);
            if (!roleResult.Succeeded) {
                logger.LogWarning("Falha ao atribuir role Member para o cliente {Email}: {Errors}", cliente.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                foreach (var error in roleResult.Errors) {
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, error.Description));
                }
                await _repository.RollbackTransactionAsync();
                return 0;
            }

            await _repository.CommitTransactionAsync();
            logger.LogInformation("Cliente {ClienteId} ({Email}) cadastrado com sucesso.", cliente.Id, cliente.Email);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao cadastrar o cliente {Email} (transação revertida).", clienteDto.Email);
            await _repository.RollbackTransactionAsync();
            throw;
        }
        return cliente.Id;
    }
}