using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public enum NotificationChannel
{
    Email,
    WhatsApp,
    Push
}

public interface INotificationService
{
    Task EnviarNotificacaoNovoPedidoAsync(Pedido pedido);

    /// <summary>Quem não cancelou fica sabendo: o cliente, se foi o admin; os admins, se foi o cliente.</summary>
    Task EnviarNotificacaoPedidoCanceladoAsync(Pedido pedido, bool canceladoPeloAdmin);

    /// <summary>Lembra o cliente de devolver hoje os jogos destes itens.</summary>
    Task EnviarLembreteDevolucaoAsync(Cliente cliente, IReadOnlyList<ItemPedido> itens);
}

public class NotificationService : INotificationService
{
    private readonly IEmailService _emailService;
    private readonly IPushService _pushService;
    private readonly ILogger<NotificationService> _logger;
    private readonly List<NotificationChannel> _activeChannels;

    public NotificationService(
        IEmailService emailService,
        IPushService pushService,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _emailService = emailService;
        _pushService = pushService;
        _logger = logger;
        _activeChannels = new List<NotificationChannel>();

        // Configuração padrão: E-mail e Push ativados por padrão, WhatsApp desativado por padrão
        var emailEnabled = configuration.GetValue<bool>("Notification:Email:Enabled", true);
        var whatsappEnabled = configuration.GetValue<bool>("Notification:WhatsApp:Enabled", false);
        var pushEnabled = configuration.GetValue<bool>("Notification:Push:Enabled", true);

        if (emailEnabled)
        {
            _activeChannels.Add(NotificationChannel.Email);
        }
        if (whatsappEnabled)
        {
            _activeChannels.Add(NotificationChannel.WhatsApp);
        }
        if (pushEnabled)
        {
            _activeChannels.Add(NotificationChannel.Push);
        }
    }

    public async Task EnviarNotificacaoNovoPedidoAsync(Pedido pedido)
    {
        var subject = $"Novo Pedido Criado - #{pedido.Id}";

        // Formatar valor usando padrão brasileiro
        var valorFormatado = pedido.ValorTotal.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
        var dataPedido = pedido.DataHora.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        var listagemJogos = "";
        if (pedido.Items != null && pedido.Items.Count > 0)
        {
            listagemJogos += "<p><strong>Jogos Alugados:</strong></p><ul>";
            foreach (var item in pedido.Items)
            {
                var nomeJogo = item.JogoCopia?.Jogo?.Nome ?? "Jogo Desconhecido";
                var valorItem = item.Valor.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
                listagemJogos += $"<li>{nomeJogo} ({valorItem})</li>";
            }
            listagemJogos += "</ul>";
        }

        var body = $@"
            <h2>Novo Pedido Cadastrado no Sistema</h2>
            <p><strong>ID do Pedido:</strong> #{pedido.Id}</p>
            <p><strong>Cliente:</strong> {pedido.Cliente?.Nome} ({pedido.Cliente?.Email})</p>
            {listagemJogos}
            <p><strong>Valor Total:</strong> {valorFormatado}</p>
            <p><strong>Método de Pagamento:</strong> {pedido.MetodoPagamento}</p>
            <p><strong>Método de Entrega:</strong> {pedido.MetodoEntrega}</p>
            <p><strong>Data/Hora:</strong> {dataPedido}</p>
        ";

        foreach (var channel in _activeChannels)
        {
            try
            {
                switch (channel)
                {
                    case NotificationChannel.Email:
                        _logger.LogInformation("Enviando notificação de novo pedido #{PedidoId} por e-mail para contato@proximoturno.com.br.", pedido.Id);
                        await _emailService.SendEmailAsync("contato@proximoturno.com.br", subject, body, isHtml: true);
                        break;

                    case NotificationChannel.WhatsApp:
                        _logger.LogWarning("Envio de notificação por WhatsApp ainda não implementado.");
                        break;

                    case NotificationChannel.Push:
                        _logger.LogInformation("Enviando notificação push do novo pedido #{PedidoId}.", pedido.Id);
                        await _pushService.EnviarParaAdminsAsync(MontarMensagemPush(pedido, valorFormatado));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação de novo pedido #{PedidoId} via canal {Channel}.", pedido.Id, channel);
            }
        }
    }

    public async Task EnviarNotificacaoPedidoCanceladoAsync(Pedido pedido, bool canceladoPeloAdmin)
    {
        if (!_activeChannels.Contains(NotificationChannel.Push))
        {
            return;
        }

        var url = $"/pedidos?ver={pedido.Id}";
        var tag = $"pedido-{pedido.Id}";
        if (canceladoPeloAdmin)
        {
            if (string.IsNullOrWhiteSpace(pedido.Cliente?.Email))
            {
                _logger.LogWarning("Pedido #{PedidoId} cancelado sem e-mail de cliente para notificar.", pedido.Id);
                return;
            }
            _logger.LogInformation("Enviando notificação push do cancelamento do pedido #{PedidoId} ao cliente.", pedido.Id);
            await _pushService.EnviarParaClienteAsync(pedido.Cliente.Email, new MensagemPush(
                Titulo: $"Pedido #{pedido.Id} cancelado",
                Corpo: $"Seu pedido #{pedido.Id} foi cancelado. Em caso de dúvida, fale com a gente.",
                Url: url,
                Tag: tag));
        }
        else
        {
            var valor = pedido.ValorTotal.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
            _logger.LogInformation("Enviando notificação push do cancelamento do pedido #{PedidoId} aos admins.", pedido.Id);
            await _pushService.EnviarParaAdminsAsync(new MensagemPush(
                Titulo: $"Pedido #{pedido.Id} cancelado pelo cliente",
                Corpo: $"{NomeCliente(pedido.Cliente)} cancelou o pedido #{pedido.Id} ({valor}).",
                Url: url,
                Tag: tag));
        }
    }

    public async Task EnviarLembreteDevolucaoAsync(Cliente cliente, IReadOnlyList<ItemPedido> itens)
    {
        if (!_activeChannels.Contains(NotificationChannel.Push) || itens.Count == 0)
        {
            return;
        }

        var nomes = itens
            .Select(i => i.JogoCopia?.Jogo?.Nome)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        var jogos = nomes.Count switch
        {
            0 => itens.Count == 1 ? "do jogo" : "dos jogos",
            1 => $"de {nomes[0]}",
            _ => $"de {string.Join(", ", nomes.Take(nomes.Count - 1))} e {nomes[^1]}"
        };

        _logger.LogInformation("Enviando lembrete de devolução ao cliente {ClienteId} ({Quantidade} itens).", cliente.Id, itens.Count);
        await _pushService.EnviarParaClienteAsync(cliente.Email, new MensagemPush(
            Titulo: "Hoje é dia de devolução",
            Corpo: $"Olá, só passando pra lembrar da devolução {jogos} hoje, mas fica tranquilo: você tem até as 22:00, então ainda dá tempo para jogar mais algumas partidas 😊",
            Url: "/pedidos",
            Tag: $"devolucao-{DateTime.Today:yyyyMMdd}"));
    }

    private static string NomeCliente(Cliente? cliente) =>
        cliente?.Nome is { Length: > 0 } nome
            ? CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(nome)
            : "Cliente";

    private static MensagemPush MontarMensagemPush(Pedido pedido, string valorFormatado)
    {
        var quantidadeJogos = pedido.Items?.Count ?? 0;
        var jogos = quantidadeJogos == 1 ? "1 jogo" : $"{quantidadeJogos} jogos";
        var cliente = NomeCliente(pedido.Cliente);

        return new MensagemPush(
            Titulo: $"Novo pedido #{pedido.Id}",
            Corpo: $"{cliente} · {jogos} · {valorFormatado}",
            Url: $"/pedidos?ver={pedido.Id}",
            Tag: $"pedido-{pedido.Id}");
    }
}
