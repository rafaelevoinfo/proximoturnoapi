using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// O PDF do manual aberto localmente: quantas páginas tem, o texto embutido de cada uma
/// (quando existe) e PDFs menores com só algumas páginas, para a extração ir por partes.
/// </summary>
public sealed class DocumentoPdf : IDisposable {

    private readonly PdfDocument _documento;
    private readonly byte[] _bytes;

    private DocumentoPdf(PdfDocument documento, byte[] bytes) {
        _documento = documento;
        _bytes = bytes;
    }

    public int TotalPaginas => _documento.NumberOfPages;

    /// <summary>Null quando o PdfPig não consegue ler o arquivo (cifrado, corrompido, formato exótico).</summary>
    public static DocumentoPdf? Abrir(byte[] bytes) {
        try {
            var documento = PdfDocument.Open(bytes);
            if (documento.NumberOfPages == 0) {
                documento.Dispose();
                return null;
            }
            return new DocumentoPdf(documento, bytes);
        } catch (Exception) {
            return null;
        }
    }

    /// <summary>Um PDF com as páginas de <paramref name="primeira"/> a <paramref name="ultima"/> (base 1, inclusivo).</summary>
    public byte[] Trecho(int primeira, int ultima) {
        // O documento inteiro vai como está: reescrever só arriscaria perder algo.
        if (primeira == 1 && ultima == TotalPaginas) {
            return _bytes;
        }

        using var construtor = new PdfDocumentBuilder();
        for (var pagina = primeira; pagina <= ultima; pagina++) {
            construtor.AddPage(_documento, pagina);
        }
        return construtor.Build();
    }

    /// <summary>
    /// Palavras da camada de texto das páginas, na ordem em que aparecem. Vazio em página
    /// que é só imagem (manual escaneado sem OCR embutido). Palavra hifenizada no fim da
    /// linha é juntada à seguinte, como o modelo vai transcrevê-la.
    /// </summary>
    public IReadOnlyList<string> Palavras(int primeira, int ultima) {
        var palavras = new List<string>();
        for (var pagina = primeira; pagina <= ultima; pagina++) {
            try {
                palavras.AddRange(_documento.GetPage(pagina).GetWords().Select(w => w.Text));
            } catch (Exception) {
                // Página que o PdfPig não interpreta conta como sem texto: a cobertura
                // dela não é medida, e vale a nota do próprio modelo.
            }
        }
        return JuntarHifenizadas(palavras);
    }

    public static List<string> JuntarHifenizadas(IReadOnlyList<string> palavras) {
        var resultado = new List<string>(palavras.Count);
        for (var i = 0; i < palavras.Count; i++) {
            var palavra = palavras[i];
            while (palavra.Length > 1 && palavra[^1] is '-' or '­' && i + 1 < palavras.Count
                   && palavras[i + 1].Length > 0 && char.IsLower(palavras[i + 1][0])) {
                palavra = palavra[..^1] + palavras[++i];
            }
            resultado.Add(palavra);
        }
        return resultado;
    }

    public void Dispose() => _documento.Dispose();
}
