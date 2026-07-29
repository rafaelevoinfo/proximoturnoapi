namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>Arquivo de dump já comprimido e cifrado, pronto para envio.</summary>
public record ResultadoDump(string CaminhoArquivo, long TamanhoBytes);

public interface IDumpBanco
{
    Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken);
}
