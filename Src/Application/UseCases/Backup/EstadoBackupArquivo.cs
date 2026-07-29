using System.Text.Json;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Guarda o estado num JSON dentro de um volume dedicado, para sobreviver a
/// reinícios do contêiner.
/// </summary>
public class EstadoBackupArquivo(string caminho) : IEstadoBackupStore
{
    public async Task<EstadoBackup?> LerAsync()
    {
        if (!File.Exists(caminho)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(caminho);
            return JsonSerializer.Deserialize<EstadoBackup>(json);
        }
        catch (JsonException)
        {
            // Arquivo corrompido não pode impedir o backup de rodar: tratamos
            // como "nunca executou" e a verificação de tamanho é ignorada.
            return null;
        }
    }

    public async Task GravarAsync(EstadoBackup estado)
    {
        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);

        await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(estado));
    }
}
