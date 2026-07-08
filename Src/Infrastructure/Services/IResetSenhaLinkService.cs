namespace ProximoTurnoApi.Infrastructure.Services;

public interface IResetSenhaLinkService
{
    Task<string?> GerarLinkAsync(string email);
}
