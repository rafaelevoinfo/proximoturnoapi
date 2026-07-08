using System.Text;
using System.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public class ResetSenhaLinkService(
    UserManager<Usuario> _userManager,
    IConfiguration _configuration,
    ILogger<ResetSenhaLinkService> logger) : IResetSenhaLinkService
{
    public async Task<string?> GerarLinkAsync(string email)
    {
        logger.LogInformation("Gerando link de reset de senha para {Email}.", email);

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            logger.LogWarning("Usuário com email {Email} não encontrado.", email);
            return null;
        }

        var configuredUrl = _configuration["FrontendUrl"];
        var frontendUrl = string.IsNullOrWhiteSpace(configuredUrl) ? "http://localhost:3000" : configuredUrl;
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetLink = $"{frontendUrl.TrimEnd('/')}/resetar-senha?token={encodedToken}&email={HttpUtility.UrlEncode(email)}";

        logger.LogInformation("Link de reset gerado com sucesso para {Email}.", email);
        return resetLink;
    }
}
