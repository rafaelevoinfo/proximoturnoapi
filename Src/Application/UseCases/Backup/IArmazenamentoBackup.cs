namespace ProximoTurnoApi.Application.UseCases.Backup;

public interface IArmazenamentoBackup
{
    Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken);

    /// <summary>Chaves já existentes sob um prefixo, sem o prefixo removido.</summary>
    Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken);
}
