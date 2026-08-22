namespace ProximoTurnoApi.Application.UseCases.RAG;

public class MarkdownExtractor(ILogger<MarkdownExtractor> _logger, ITextExtractor _textExtractor) {
    public async Task<string> ExtractMarkdownAsync(string filePath) {
        var markdownFilePath = Path.ChangeExtension(filePath, ".md");
        if (!File.Exists(markdownFilePath)) {
            return await _textExtractor.ExtractTextAsync(filePath, CancellationToken.None);
        } else {
            _logger.LogDebug("Markdown já extraído para {FilePath}.", markdownFilePath);
        }
        return markdownFilePath;
    }
}