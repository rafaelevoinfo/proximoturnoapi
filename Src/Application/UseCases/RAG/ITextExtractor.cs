namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Texto do manual e o rastro de quem o produziu. O arquivo é gravado por quem chama:
/// o cache é por hash do conteúdo do PDF, e o extrator não conhece essa regra.
/// </summary>
public sealed record ResultadoExtracao(string Texto, string Modelo, int Confiabilidade);

public interface ITextExtractor {
    Task<ResultadoExtracao> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}