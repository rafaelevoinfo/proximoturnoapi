
namespace ProximoTurnoApi.Application.UseCases.RAG;

public interface ITextExtractor {
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}