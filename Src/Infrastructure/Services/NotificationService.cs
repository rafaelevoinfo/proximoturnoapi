using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;
    private readonly List<NotificationChannel> _activeChannels;

    public NotificationService(
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _emailService = emailService;
        _logger = logger;
        _activeChannels = new List<NotificationChannel>();

        // Configuração padrão: Envio por E-mail ativado por padrão, WhatsApp desativado por padrão
        var emailEnabled = configuration.GetValue<bool>("Notification:Email:Enabled", true);
        var whatsappEnabled = configuration.GetValue<bool>("Notification:WhatsApp:Enabled", false);

        if (emailEnabled)
        {
            _activeChannels.Add(NotificationChannel.Email);
        }
        if (whatsappEnabled)
        {
            _activeChannels.Add(NotificationChannel.WhatsApp);
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação de novo pedido #{PedidoId} via canal {Channel}.", pedido.Id, channel);
            }
        }
    }
}
