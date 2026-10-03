using Microsoft.Extensions.Configuration;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Onde o sink de arquivo do Serilog grava. Derivado da própria seção "Serilog" do
/// appsettings para a tela de logs nunca divergir do caminho configurado lá.
/// </summary>
public class ArquivosLogOptions {
    public string Diretorio { get; init; } = "";
    /// <summary>Nome base do arquivo sem extensão ("log" em "logs/log.log").</summary>
    public string Prefixo { get; init; } = "log";
    public string Extensao { get; init; } = ".log";

    public const string CaminhoPadrao = "logs/log.log";

    /// <summary>
    /// O sink resolve caminhos relativos pelo diretório atual do processo (no contêiner,
    /// o WORKDIR /app), não pela raiz de conteúdo; por isso o mesmo aqui.
    /// </summary>
    public static ArquivosLogOptions DaConfiguracao(IConfiguration configuration, string diretorioAtual) {
        var caminho = configuration.GetSection("Serilog:WriteTo").GetChildren()
            .Where(s => string.Equals(s["Name"], "File", StringComparison.OrdinalIgnoreCase))
            .Select(s => s["Args:path"])
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))
            ?? CaminhoPadrao;

        var completo = Path.GetFullPath(caminho, diretorioAtual);
        return new ArquivosLogOptions {
            Diretorio = Path.GetDirectoryName(completo) ?? diretorioAtual,
            Prefixo = Path.GetFileNameWithoutExtension(completo),
            Extensao = Path.GetExtension(completo),
        };
    }
}
