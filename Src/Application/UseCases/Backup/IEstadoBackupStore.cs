namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>Resultado da última execução bem-sucedida.</summary>
public record EstadoBackup(DateTime UltimaExecucaoUtc, long TamanhoDumpBytes);

public interface IEstadoBackupStore
{
    Task<EstadoBackup?> LerAsync();
    Task GravarAsync(EstadoBackup estado);
}
