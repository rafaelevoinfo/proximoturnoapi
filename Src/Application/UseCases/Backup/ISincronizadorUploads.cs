namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Números absolutos de uma sincronização, além do delta enviado agora. Os
/// totais existem para que um "0 locais" inesperado (ex.: volume de uploads
/// não montado) fique visível no e-mail de sucesso — um delta de zero
/// sozinho é idêntico tanto numa noite tranquila quanto num volume ausente.
/// </summary>
public record ResultadoSincronizacao(int NovosEnviados, int TotalArquivosLocais, int TotalChavesRemotas);

public interface ISincronizadorUploads
{
    /// <summary>Envia apenas os arquivos ausentes no bucket. Retorna os totais absolutos e quantos foram enviados agora.</summary>
    Task<ResultadoSincronizacao> SincronizarAsync(CancellationToken cancellationToken);
}
