using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class RegistrarInscricaoPush(IInscricaoPushRepository _repository, ILogger<RegistrarInscricaoPush> logger) : UseCaseBasico {

    public async Task<bool> ExecuteAsync(string idUsuario, InscricaoPushDTO dto, string? userAgent) {
        // O servidor faz POST no endpoint a cada notificação: só aceita URL https de verdade.
        if (!Uri.TryCreate(dto.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Endpoint de notificação inválido."));
        } else if (dto.Endpoint.Length > 600) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Endpoint de notificação muito longo."));
        }
        if (string.IsNullOrWhiteSpace(dto.P256dh) || dto.P256dh.Length > 200 ||
            string.IsNullOrWhiteSpace(dto.Auth) || dto.Auth.Length > 100) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Chaves da inscrição inválidas."));
        }
        if (!IsValid) {
            return false;
        }

        // O endpoint é do navegador, não do usuário: se outra pessoa logar no mesmo aparelho,
        // a inscrição passa para ela, com as chaves que o navegador mandar agora.
        var inscricao = await _repository.GetByEndpointAsync(dto.Endpoint);
        if (inscricao is null) {
            inscricao = new InscricaoPush {
                IdUsuario = idUsuario,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
                DataCriacao = DateTime.Now
            };
        } else {
            inscricao.IdUsuario = idUsuario;
            inscricao.P256dh = dto.P256dh;
            inscricao.Auth = dto.Auth;
        }
        inscricao.UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;

        await _repository.SalvarAsync(inscricao);
        logger.LogInformation("Inscrição push {IdInscricao} registrada para o usuário {IdUsuario}.", inscricao.Id, idUsuario);
        return true;
    }
}
