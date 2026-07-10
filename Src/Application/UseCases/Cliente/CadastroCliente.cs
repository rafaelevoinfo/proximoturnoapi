using System.Web;
using Flunt.Notifications;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCliente(
    IClienteRepository _repository,
    UserManager<Usuario> _userManager,
    IResetSenhaLinkService _resetSenhaLinkService,
    IEmailService _emailService,
    ILogger<CadastroCliente> logger) : UseCaseBasico {
    public async Task<int> ExecuteAsync(ClienteDTO clienteDto, bool enviarEmailAtivacao = true) {
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

        // O CPF é opcional, então só é comparado quando informado: sem isso, todos os clientes
        // antigos (que têm CPF nulo) colidiriam entre si.
        if (!string.IsNullOrEmpty(clienteDto.Cpf)) {
            filtro = new FiltroClienteDTO {
                Cpf = clienteDto.Cpf
            };

            clientesExistentes = await _repository.GetAllAsync(filtro);
            if (clientesExistentes.Count > 0) {
                logger.LogWarning("Falha ao cadastrar cliente {Email}: Já existe um cliente com o CPF informado.", clienteDto.Email);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe um cliente com o mesmo CPF."));
            }
        }

        if (!IsValid)
            return 0;

        var cliente = clienteDto.ToModel();
        await _repository.StartTransactionAsync();
        try {
            await _repository.AddAsync(cliente);

            // Usuário é criado sem senha: a conta fica inutilizável até o cliente
            // definir a senha através do link de ativação enviado por email.
            var usuario = new Usuario() {
                UserName = cliente.Email,
                Email = cliente.Email,
                Nome = cliente.Nome,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(usuario);
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

        if (enviarEmailAtivacao) {
            await EnviarEmailAtivacaoAsync(cliente);
        } else {
            // Sem o e-mail, a conta permanece sem senha: o cliente só consegue acessá-la
            // solicitando "esqueci minha senha" ou recebendo um novo link de ativação.
            logger.LogInformation("E-mail de ativação suprimido para o cliente {ClienteId} ({Email}).", cliente.Id, cliente.Email);
        }

        return cliente.Id;
    }

    private async Task EnviarEmailAtivacaoAsync(Cliente cliente) {
        try {
            var link = await _resetSenhaLinkService.GerarLinkAsync(cliente.Email, "/ativar-conta");
            if (link == null) {
                logger.LogWarning("Não foi possível gerar o link de ativação para {Email}.", cliente.Email);
                return;
            }

            var displayName = HttpUtility.HtmlEncode(cliente.Nome);
            var encodedLink = HttpUtility.HtmlEncode(link);
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                    <h2 style='color: #581c87; text-align: center;'>Bem-vindo à Próximo Turno!</h2>
                    <p>Olá, <strong>{displayName}</strong>,</p>
                    <p>Seu cadastro foi realizado com sucesso. Para ativar sua conta e criar sua senha de acesso, clique no botão abaixo:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{encodedLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Ativar Conta</a>
                    </div>
                    <p style='color: #666; font-size: 12px; text-align: center;'>Se você não solicitou este cadastro, desconsidere este e-mail.</p>
                </div>";

            await _emailService.SendEmailAsync(cliente.Email, "Ative sua Conta - Próximo Turno", body, isHtml: true);
        } catch (Exception ex) {
            logger.LogError(ex, "Falha ao enviar e-mail de ativação para {Email}.", cliente.Email);
        }
    }
}
