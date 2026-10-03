using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Quanto do texto embutido no PDF aparece no markdown extraído. É a medida objetiva de
/// "faltou coisa": a nota que o próprio modelo dá não serve, porque ele não sabe o que pulou.
/// </summary>
public static partial class CoberturaTexto {

    /// <summary>
    /// Abaixo disso a página praticamente não tem camada de texto (é imagem): a cobertura
    /// não diz nada e quem decide é a nota do modelo.
    /// </summary>
    public const int MinimoTermosReferencia = 40;

    // Palavras curtas ("de", "a", "o") aparecem em qualquer texto e só inflariam a cobertura.
    private const int TamanhoMinimoTermo = 3;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Termo();

    /// <summary>
    /// Fração (0 a 1) dos termos distintos da referência que estão no markdown, ou null quando
    /// a referência tem termos de menos para medir.
    /// </summary>
    public static double? Calcular(IEnumerable<string> palavrasReferencia, string markdown) {
        var referencia = Termos(string.Join(' ', palavrasReferencia));
        if (referencia.Count < MinimoTermosReferencia) {
            return null;
        }

        var extraidos = Termos(markdown);
        return (double)referencia.Count(extraidos.Contains) / referencia.Count;
    }

    /// <summary>
    /// Termos distintos, sem acento e em minúsculas. Sem acento porque o OCR e a camada de
    /// texto erram acentuação de forma diferente; FormKC desfaz ligaduras ("ﬁ" -> "fi").
    /// </summary>
    public static HashSet<string> Termos(string texto) {
        var normalizado = texto.Normalize(NormalizationForm.FormKC).Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) {
                semAcento.Append(char.ToLowerInvariant(c));
            }
        }

        return Termo().Matches(semAcento.ToString())
            .Select(m => m.Value)
            .Where(t => t.Length >= TamanhoMinimoTermo)
            .ToHashSet();
    }
}
