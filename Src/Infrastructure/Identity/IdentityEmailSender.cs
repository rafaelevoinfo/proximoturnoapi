using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Infrastructure.Identity;

public class IdentityEmailSender : IEmailSender<Usuario>
{
    private readonly IEmailService _emailService;

    public IdentityEmailSender(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendPasswordResetLinkAsync(Usuario user, string email, string resetLink)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Recuperação de Senha - Próximo Turno</h2>
                <p>Olá, <strong>{user.Nome ?? email}</strong>,</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no Próximo Turno.</p>
                <p>Clique no botão abaixo para prosseguir com a redefinição:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Redefinir Senha</a>
                </div>
                <p style='color: #666; font-size: 12px; text-align: center;'>Se você não solicitou esta alteração, desconsidere este e-mail.</p>
            </div>";

        await _emailService.SendEmailAsync(email, "Recuperação de Senha - Próximo Turno", body, isHtml: true);
    }

    public async Task SendPasswordResetCodeAsync(Usuario user, string email, string resetCode)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Recuperação de Senha - Próximo Turno</h2>
                <p>Olá, <strong>{user.Nome ?? email}</strong>,</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no Próximo Turno.</p>
                <p>Use o código abaixo para completar o processo:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <span style='background-color: #f3e8ff; color: #581c87; font-size: 24px; font-weight: bold; padding: 10px 20px; border-radius: 5px; letter-spacing: 2px; border: 1px dashed #581c87;'>{resetCode}</span>
                </div>
                <p style='color: #666; font-size: 12px; text-align: center;'>Se você não solicitou esta alteração, desconsidere este e-mail.</p>
            </div>";

        await _emailService.SendEmailAsync(email, "Recuperação de Senha - Próximo Turno", body, isHtml: true);
    }

    public async Task SendConfirmationLinkAsync(Usuario user, string email, string confirmationLink)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                <h2 style='color: #581c87; text-align: center;'>Bem-vindo ao Próximo Turno!</h2>
                <p>Olá, <strong>{user.Nome ?? email}</strong>,</p>
                <p>Obrigado por se cadastrar. Clique no botão abaixo para confirmar seu e-mail:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{confirmationLink}' style='background-color: #581c87; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Confirmar Cadastro</a>
                </div>
            </div>";

        await _emailService.SendEmailAsync(email, "Confirmação de Conta - Próximo Turno", body, isHtml: true);
    }
}
