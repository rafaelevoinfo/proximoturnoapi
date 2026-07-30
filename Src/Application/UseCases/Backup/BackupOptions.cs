using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Opções do backup automatizado. Todos os valores não sensíveis têm padrão
/// embutido; apenas os três segredos precisam vir do ambiente.
/// </summary>
public class BackupOptions {
    public bool Habilitado { get; init; } = true;
    public TimeSpan Horario { get; init; } = new(3, 0, 0);
    public string EmailDestino { get; init; } = "contato@proximoturno.com.br";
    public string B2Endpoint { get; init; } = "https://s3.us-east-005.backblazeb2.com";
    public string B2Bucket { get; init; } = "proximo-turno";
    public string CaminhoUploads { get; init; } = "/app/wwwroot/uploads";
    public string CaminhoEstado { get; init; } = "/app/backup-state/ultimo-backup.json";

    public string? Passphrase { get; init; }
    public string? B2KeyId { get; init; }
    public string? B2ApplicationKey { get; init; }

    /// <summary>
    /// Região a usar no escopo de credencial da assinatura SigV4. A Backblaze
    /// valida a região contra o endpoint (ex.: "us-east-005"); se o SDK
    /// assinar com a região padrão ("us-east-1"), toda chamada falha com
    /// SignatureDoesNotMatch/403. Derivada de <see cref="B2Endpoint"/> em vez
    /// de fixa, para nunca divergir do endpoint configurado. Null quando o
    /// endpoint não segue o formato esperado — nesse caso deixamos o SDK usar
    /// seu padrão em vez de forçar algo possivelmente errado.
    /// </summary>
    public string? RegiaoAutenticacao => RegiaoDoEndpoint(B2Endpoint);

    /// <summary>
    /// Extrai o segmento de região de uma URL no formato
    /// "https://s3.&lt;regiao&gt;.backblazeb2.com" (com ou sem barra final).
    /// Retorna null se a URL não seguir esse formato.
    /// </summary>
    private static string? RegiaoDoEndpoint(string? endpoint) {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;

        var match = Regex.Match(endpoint, @"^https?://s3\.([^./]+)\.backblazeb2\.com/?$");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Sem os três segredos o backup não tem como cifrar nem enviar, então o
    /// serviço nem chega a agendar execuções.
    /// </summary>
    public bool SegredosPresentes =>
        !string.IsNullOrWhiteSpace(Passphrase) &&
        !string.IsNullOrWhiteSpace(B2KeyId) &&
        !string.IsNullOrWhiteSpace(B2ApplicationKey);

    public static BackupOptions DaConfiguracao(IConfiguration configuration) {
        var padrao = new BackupOptions();

        return new BackupOptions {
            Habilitado = LerBool(configuration["BACKUP_ENABLED"], padrao.Habilitado),
            Horario = LerHorario(configuration["BACKUP_HORA"], padrao.Horario),
            EmailDestino = LerTexto(configuration["BACKUP_EMAIL_DESTINO"], padrao.EmailDestino),
            B2Endpoint = LerTexto(configuration["B2_ENDPOINT"], padrao.B2Endpoint),
            B2Bucket = LerTexto(configuration["B2_BUCKET"], padrao.B2Bucket),
            CaminhoUploads = LerTexto(configuration["BACKUP_CAMINHO_UPLOADS"], padrao.CaminhoUploads),
            CaminhoEstado = LerTexto(configuration["BACKUP_CAMINHO_ESTADO"], padrao.CaminhoEstado),
            Passphrase = configuration["BACKUP_PASSPHRASE"],
            B2KeyId = configuration["B2_KEY_ID"],
            B2ApplicationKey = configuration["B2_APPLICATION_KEY"]
        };
    }

    private static string LerTexto(string? valor, string padrao) =>
        string.IsNullOrWhiteSpace(valor) ? padrao : valor;

    private static bool LerBool(string? valor, bool padrao) =>
        bool.TryParse(valor, out var resultado) ? resultado : padrao;

    private static TimeSpan LerHorario(string? valor, TimeSpan padrao) =>
        TimeSpan.TryParse(valor, out var resultado) ? resultado : padrao;
}
