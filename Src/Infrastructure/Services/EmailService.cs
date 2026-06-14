using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProximoTurnoApi.Infrastructure.Services;

public class EmailService : IEmailService {
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger) {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true) {
        var host = _configuration["SMTP_HOST"];
        var portStr = _configuration["SMTP_PORT"];
        var username = _configuration["SMTP_USERNAME"];
        var password = _configuration["SMTP_PASSWORD"];
        var useSslStr = _configuration["SMTP_USE_SSL"];
        var fromName = _configuration["SMTP_FROM_NAME"] ?? "Próximo Turno";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(portStr) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) {
            _logger.LogWarning("Configurações SMTP incompletas. Envio de e-mail abortado.");
            return;
        }

        int.TryParse(portStr, out var port);
        bool.TryParse(useSslStr, out var useSsl);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, username));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        if (isHtml) {
            bodyBuilder.HtmlBody = body;
        } else {
            bodyBuilder.TextBody = body;
        }
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try {
            var secureSocketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, secureSocketOptions);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            _logger.LogInformation("E-mail enviado com sucesso para {ToEmail}", toEmail);
        } catch (Exception ex) {
            _logger.LogError(ex, "Falha ao enviar e-mail para {ToEmail}", toEmail);
            throw;
        }
        finally {
            await client.DisconnectAsync(true);
        }
    }
}
