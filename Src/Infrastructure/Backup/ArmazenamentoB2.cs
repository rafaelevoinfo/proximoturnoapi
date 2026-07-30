using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Armazenamento na Backblaze B2 pela API S3-compatível. A Backblaze exige
/// path-style; virtual-host style não funciona no endpoint deles.
/// </summary>
public class ArmazenamentoB2 : IArmazenamentoBackup, IDisposable {
    private readonly IAmazonS3 _cliente;
    private readonly BackupOptions _options;
    private readonly ILogger<ArmazenamentoB2> _logger;

    public ArmazenamentoB2(BackupOptions options, ILogger<ArmazenamentoB2> logger) {
        _options = options;
        _logger = logger;

        var config = new AmazonS3Config {
            ServiceURL = options.B2Endpoint,
            ForcePathStyle = true
        };

        // A Backblaze valida a região no escopo de credencial do SigV4 contra
        // o endpoint; sem isso o SDK assina com a região padrão do cliente
        // ("us-east-1"), o que resulta em SignatureDoesNotMatch/403 sempre
        // que o endpoint não for us-east-1. Inofensivo se o padrão já
        // funcionasse, decisivo quando não funciona — e não custa nada
        // deixar sem efeito (null) se o endpoint não seguir o formato
        // esperado.
        if (options.RegiaoAutenticacao is { } regiao) {
            config.AuthenticationRegion = regiao;
        }

        _cliente = new AmazonS3Client(options.B2KeyId, options.B2ApplicationKey, config);
    }

    public async Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken) {
        await _cliente.PutObjectAsync(new PutObjectRequest {
            BucketName = _options.B2Bucket,
            Key = chave,
            FilePath = caminhoLocal
        }, cancellationToken);

        _logger.LogDebug("Objeto {Chave} enviado para o bucket {Bucket}.", chave, _options.B2Bucket);
    }

    public async Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken) {
        var chaves = new List<string>();
        string? continuacao = null;

        do {
            var resposta = await _cliente.ListObjectsV2Async(new ListObjectsV2Request {
                BucketName = _options.B2Bucket,
                Prefix = prefixo,
                ContinuationToken = continuacao
            }, cancellationToken);

            // O AWSSDK v4 não inicializa coleções da resposta automaticamente
            // (AWSConfigs.InitializeCollections é false por padrão): quando o
            // prefixo não tem nenhum objeto, S3Objects vem null em vez de uma
            // lista vazia. Não "simplificar" removendo o "?? []" — isso derruba
            // a sincronização inteira na primeira noite com o prefixo vazio.
            chaves.AddRange((resposta.S3Objects ?? []).Select(o => o.Key));
            continuacao = resposta.IsTruncated == true ? resposta.NextContinuationToken : null;
        }
        while (continuacao is not null);

        return chaves;
    }

    public void Dispose() => _cliente.Dispose();
}
