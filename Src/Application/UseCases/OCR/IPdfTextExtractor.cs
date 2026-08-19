
namespace ProximoTurnoApi.Application.UseCases.OCR;

public interface IPdfTextExtractor {
    Task<string> ExtractTextAsync(string pdfFilePath, CancellationToken cancellationToken);
}