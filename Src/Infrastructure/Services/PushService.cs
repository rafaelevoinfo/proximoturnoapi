using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Infrastructure.Services;

/// <summary>O que aparece na notificação. <see cref="Url"/> é a página aberta ao tocar nela.</summary>
public record MensagemPush(string Titulo, string Corpo, string Url, string? Tag = null);

public enum ResultadoEnvioPush {
    Entregue,
    /// <summary>O serviço de push respondeu 404/410: o navegador cancelou a inscrição.</summary>
    InscricaoExpirada,
    Falha
}

/// <summary>Chaves VAPID, que identificam este servidor perante os serviços de push.</summary>
public record ConfiguracaoPush(string? ChavePublica, string? ChavePrivada, string Assunto) {
    public bool Configurado => !string.IsNullOrWhiteSpace(ChavePublica) && !string.IsNullOrWhiteSpace(ChavePrivada);

    public static ConfiguracaoPush DaConfiguracao(IConfiguration configuration) => new(
        configuration["VAPID_PUBLIC_KEY"],
        configuration["VAPID_PRIVATE_KEY"],
        configuration["VAPID_SUBJECT"] is { Length: > 0 } assunto ? assunto : "mailto:contato@proximoturno.com.br");
}

/// <summary>Uma entrega a um único navegador. Separado para os testes não precisarem de rede.</summary>
public interface IEnviadorWebPush {
    Task<ResultadoEnvioPush> EnviarAsync(InscricaoPush inscricao, string payload);
}

public interface IPushService {
    Task EnviarParaAdminsAsync(MensagemPush mensagem);
    Task EnviarParaClienteAsync(string email, MensagemPush mensagem);
}

public class PushService(
    IInscricaoPushRepository _repository,
    IEnviadorWebPush _enviador,
    ConfiguracaoPush _configuracao,
    ILogger<PushService> logger) : IPushService {

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    public Task EnviarParaAdminsAsync(MensagemPush mensagem) =>
        EnviarAsync(_repository.GetDosAdminsAsync, mensagem, "admins");

    public Task EnviarParaClienteAsync(string email, MensagemPush mensagem) =>
        EnviarAsync(() => _repository.GetDoUsuarioPorEmailAsync(email), mensagem, "cliente");

    private async Task EnviarAsync(Func<Task<List<InscricaoPush>>> buscarInscricoes, MensagemPush mensagem, string destino) {
        if (!_configuracao.Configurado) {
            logger.LogWarning("Chaves VAPID não configuradas. Notificação push não enviada.");
            return;
        }

        var inscricoes = await buscarInscricoes();
        if (inscricoes.Count == 0) {
            logger.LogInformation("Nenhum aparelho inscrito para notificações push ({Destino}).", destino);
            return;
        }

        // Os nomes batem com o que o public/sw.js do portal lê.
        var payload = JsonSerializer.Serialize(new {
            title = mensagem.Titulo,
            body = mensagem.Corpo,
            url = mensagem.Url,
            tag = mensagem.Tag
        }, OpcoesJson);

        foreach (var inscricao in inscricoes) {
            var resultado = await _enviador.EnviarAsync(inscricao, payload);
            if (resultado == ResultadoEnvioPush.InscricaoExpirada) {
                logger.LogInformation("Inscrição push {IdInscricao} expirou e foi removida.", inscricao.Id);
                await _repository.RemoverPorIdAsync(inscricao.Id);
            }
        }
    }
}

public class EnviadorWebPush : IEnviadorWebPush {
    private readonly PushServiceClient _cliente;
    private readonly ILogger<EnviadorWebPush> _logger;

    public EnviadorWebPush(HttpClient httpClient, ConfiguracaoPush configuracao, ILogger<EnviadorWebPush> logger) {
        _logger = logger;
        _cliente = new PushServiceClient(httpClient);
        if (configuracao.Configurado) {
            _cliente.DefaultAuthentication = new VapidAuthentication(configuracao.ChavePublica, configuracao.ChavePrivada) {
                Subject = configuracao.Assunto
            };
        }
    }

    public async Task<ResultadoEnvioPush> EnviarAsync(InscricaoPush inscricao, string payload) {
        var assinatura = new PushSubscription { Endpoint = inscricao.Endpoint };
        assinatura.SetKey(PushEncryptionKeyName.P256DH, inscricao.P256dh);
        assinatura.SetKey(PushEncryptionKeyName.Auth, inscricao.Auth);

        // Um pedido parado num celular desligado por mais de um dia já não é novidade.
        var mensagem = new PushMessage(payload) {
            Urgency = PushMessageUrgency.High,
            TimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds
        };

        try {
            await _cliente.RequestPushMessageDeliveryAsync(assinatura, mensagem);
            return ResultadoEnvioPush.Entregue;
        } catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) {
            return ResultadoEnvioPush.InscricaoExpirada;
        } catch (Exception ex) {
            _logger.LogError(ex, "Falha ao enviar notificação push para a inscrição {IdInscricao}.", inscricao.Id);
            return ResultadoEnvioPush.Falha;
        }
    }
}
