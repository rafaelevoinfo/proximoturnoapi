using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class EnviarEmailsClientes(
    IClienteRepository _clienteRepository,
    IEmailService _emailService,
    IResetSenhaLinkService _resetSenhaLinkService,
    ILogger<EnviarEmailsClientes> logger) : UseCaseBasico
{
    public async Task<bool> ExecuteAsync(EnviarEmailsClientesRequest request)
    {
        logger.LogInformation("Iniciando envio de email para {Count} clientes.", request.ClienteIds.Count);

        var clientes = await _clienteRepository.GetAllByIdsAsync(request.ClienteIds);
        if (clientes.Count == 0)
        {
            logger.LogWarning("Nenhum cliente encontrado para os IDs fornecidos.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum cliente encontrado."));
            return false;
        }

        var enviados = 0;
        var falhas = 0;

        foreach (var cliente in clientes)
        {
            try
            {
                var resetLink = await _resetSenhaLinkService.GerarLinkAsync(cliente.Email);

                var titulo = request.Titulo
                    .Replace("{cliente_nome}", cliente.Nome);

                var conteudo = request.Conteudo
                    .Replace("{cliente_nome}", cliente.Nome)
                    .Replace("{link_resetar_senha}", resetLink ?? "");

                await _emailService.SendEmailAsync(cliente.Email, titulo, conteudo, true);
                enviados++;
                logger.LogInformation("Email enviado para {Email} (cliente {ClienteId}).", cliente.Email, cliente.Id);
            }
            catch (Exception ex)
            {
                falhas++;
                logger.LogError(ex, "Falha ao enviar email para {Email} (cliente {ClienteId}).", cliente.Email, cliente.Id);
            }
        }

        logger.LogInformation("Envio concluído. {Enviados} enviados, {Falhas} falhas.", enviados, falhas);
        return true;
    }
}
