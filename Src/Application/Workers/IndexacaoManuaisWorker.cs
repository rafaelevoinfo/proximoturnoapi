using ProximoTurnoApi.Application.UseCases.OCR;

namespace ProximoTurnoApi.Application.Workers;

public class IndexacaoManuaisWorker : BackgroundService {
    private FileSystemWatcher _fileWatcher = null;
    private IPdfTextExtractor _pdfTextExtractor;

    public IndexacaoManuaisWorker(IWebHostEnvironment _env, IPdfTextExtractor pdfTextExtractor) {
        _pdfTextExtractor = pdfTextExtractor;
        _fileWatcher = new(Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads")) {
            Filter = "*.pdf"
        };
        _fileWatcher.Created += OnFileCreated;
        _fileWatcher.EnableRaisingEvents = true;
    }
    protected override Task ExecuteAsync(CancellationToken stoppingToken) {
        //Varre os arquivos existentes e verificar se o arquivo .md existe
        var existingFiles = Directory.GetFiles(_fileWatcher.Path, "*.pdf");
        foreach (var file in existingFiles) {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var mdFileName = $"{fileName}.md";
            var mdFilePath = Path.Combine(_fileWatcher.Path, mdFileName);
            if (!File.Exists(mdFilePath)) {
                ExtractTextFromPDF(file);
            }
        }
        return Task.CompletedTask;
    }

    private void ExtractTextFromPDF(string pdfFilePath) {
        // Lógica para extrair texto do arquivo PDF
        // Você pode usar bibliotecas como iTextSharp ou PdfSharp para extrair o texto
        // Exemplo:
        // var textoExtraido = _pdfTextExtractor.ExtractTextAsync(pdfFilePath);
        // Salvar ou processar o texto extraído conforme necessário
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e) {
        if (File.Exists(e.FullPath) && Path.GetExtension(e.FullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) {
            ExtractTextFromPDF(e.FullPath);
        }
    }
}