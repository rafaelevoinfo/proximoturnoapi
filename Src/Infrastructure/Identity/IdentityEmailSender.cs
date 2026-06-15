using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using System.Web;

namespace ProximoTurnoApi.Infrastructure.Identity;

public class IdentityEmailSender : IEmailSender<Usuario> {
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public IdentityEmailSender(IEmailService emailService, IConfiguration configuration) {
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task SendPasswordResetLinkAsync(Usuario user, string email, string resetLink) {
        var displayName = HttpUtility.HtmlEncode(user.Nome ?? email);
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Recuperação de Senha - Próximo Turno</h2>
                <p>Olá, <strong>{displayName}</strong>,</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no Próximo Turno.</p>
                <p>Clique no botão abaixo para prosseguir com a redefinição:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Redefinir Senha</a>
                </div>
                <p style='color: #666; font-size: 12px; text-align: center;'>Se você não solicitou esta alteração, desconsidere este e-mail.</p>
            </div>";

        await _emailService.SendEmailAsync(email, "Recuperação de Senha - Próximo Turno", body, isHtml: true);
    }

    public async Task SendPasswordResetCodeAsync(Usuario user, string email, string resetCode) {
        var configuredUrl = _configuration["FrontendUrl"];
        var frontendUrl = string.IsNullOrWhiteSpace(configuredUrl) ? "http://localhost:3000" : configuredUrl;
        var resetLink = $"{frontendUrl.TrimEnd('/')}/resetar-senha?token={HttpUtility.UrlEncode(resetCode)}&email={HttpUtility.UrlEncode(email)}";
        var displayName = HttpUtility.HtmlEncode(user.Nome ?? email);

        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Recuperação de Senha - Próximo Turno</h2>
                <p>Olá, <strong>{displayName}</strong>,</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no Próximo Turno.</p>
                <p>Clique no botão abaixo para prosseguir com a redefinição:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Redefinir Senha</a>
                </div>
                <p style='color: #666; font-size: 12px; text-align: center;'>Se você não conseguir clicar no botão, copie e cole o link abaixo no seu navegador:</p>
                <p style='color: #581c87; font-size: 12px; word-break: break-all; text-align: center;'><a href='{resetLink}'>{resetLink}</a></p>
                <p style='color: #666; font-size: 12px; text-align: center;'>Se você não solicitou esta alteração, desconsidere este e-mail.</p>
            </div>";

        await _emailService.SendEmailAsync(email, "Recuperação de Senha - Próximo Turno", body, isHtml: true);
    }

    public async Task SendConfirmationLinkAsync(Usuario user, string email, string confirmationLink) {
        var displayName = HttpUtility.HtmlEncode(user.Nome ?? email);
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Bem-vindo ao Próximo Turno!</h2>
                <p>Olá, <strong>{displayName}</strong>,</p>
                <p>Obrigado por se cadastrar. Clique no botão abaixo para confirmar seu e-mail:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{confirmationLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Confirmar Cadastro</a>
                </div>
            </div>";

        await _emailService.SendEmailAsync(email, "Confirmação de Conta - Próximo Turno", body, isHtml: true);
    }
}
