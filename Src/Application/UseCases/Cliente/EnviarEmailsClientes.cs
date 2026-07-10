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
    private const string PlaceholderResetSenha = "{link_resetar_senha}";
    private const string PlaceholderAtivarConta = "{link_ativar_conta}";

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

        var usaResetSenha = request.Conteudo.Contains(PlaceholderResetSenha);
        var usaAtivarConta = request.Conteudo.Contains(PlaceholderAtivarConta);

        foreach (var cliente in clientes)
        {
            try
            {
                var resetLink = usaResetSenha ? await _resetSenhaLinkService.GerarLinkAsync(cliente.Email) : null;
                var ativarLink = usaAtivarConta ? await _resetSenhaLinkService.GerarLinkAsync(cliente.Email, "/ativar-conta") : null;

                // Sem o link, o placeholder viraria string vazia e o cliente receberia um botão morto.
                // Preferimos não enviar e contabilizar como falha, para que o problema apareça no log.
                if ((usaResetSenha && resetLink is null) || (usaAtivarConta && ativarLink is null))
                {
                    falhas++;
                    logger.LogWarning("Email não enviado para {Email} (cliente {ClienteId}): não foi possível gerar o link solicitado.", cliente.Email, cliente.Id);
                    continue;
                }

                var titulo = request.Titulo
                    .Replace("{cliente_nome}", cliente.Nome);

                var conteudo = request.Conteudo
                    .Replace("{cliente_nome}", cliente.Nome)
                    .Replace(PlaceholderResetSenha, resetLink ?? "")
                    .Replace(PlaceholderAtivarConta, ativarLink ?? "");

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
