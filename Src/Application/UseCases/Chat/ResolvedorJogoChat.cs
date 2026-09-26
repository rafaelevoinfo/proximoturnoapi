using System.Globalization;
using System.Text;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Acha no catálogo os jogos que batem com o nome que o usuário escreveu. O catálogo é
/// pequeno (dezenas de jogos), então a comparação é em memória e tolera acento, caixa,
/// subtítulo e erro de digitação. Quem decide é o usuário: isto só monta as opções.
/// </summary>
public static class ResolvedorJogoChat {

    public const int MaximoOpcoes = 3;

    // Abaixo disso a semelhanca e acaso: "War" nao pode virar opcao para "Wingspan".
    private const double Corte = 0.6;

    private static readonly HashSet<string> Irrelevantes = ["o", "a", "os", "as", "de", "do", "da", "dos", "das", "e", "the", "of", "jogo"];

    public static List<JogoChat> Candidatos(string? mencionado, IReadOnlyList<JogoChat> jogos, int maximo = MaximoOpcoes) {
        var alvo = Normalizar(mencionado);
        if (alvo.Length == 0) {
            return [];
        }

        return [.. jogos
            .Select(jogo => (jogo, nota: Nota(alvo, Normalizar(jogo.Nome))))
            .Where(par => par.nota >= Corte)
            .OrderByDescending(par => par.nota)
            .ThenBy(par => par.jogo.Nome.Length)
            .Take(maximo)
            .Select(par => par.jogo)];
    }

    /// <summary>
    /// 1 para nome igual; alta quando um contém o outro ("catan" em "catan: cidades e
    /// cavaleiros"); senão, a melhor entre sobreposição de palavras e semelhança de grafia.
    /// </summary>
    public static double Nota(string alvo, string nome) {
        if (nome.Length == 0) {
            return 0;
        }

        if (alvo == nome) {
            return 1;
        }

        if (ContemPalavras(nome, alvo) || ContemPalavras(alvo, nome)) {
            // Quanto mais do nome o texto cobre, mais perto de 1: "catan" prefere "Catan" a
            // "Catan: Cidades e Cavaleiros".
            var cobertura = (double)Math.Min(alvo.Length, nome.Length) / Math.Max(alvo.Length, nome.Length);
            return 0.8 + 0.15 * cobertura;
        }

        return Math.Max(SobreposicaoPalavras(alvo, nome), SemelhancaGrafia(alvo, nome));
    }

    public static string Normalizar(string? texto) {
        if (string.IsNullOrWhiteSpace(texto)) {
            return "";
        }

        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var saida = new StringBuilder(decomposto.Length);
        var espaco = false;

        foreach (var c in decomposto) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) {
                continue;
            }

            if (char.IsLetterOrDigit(c)) {
                saida.Append(char.ToLowerInvariant(c));
                espaco = false;
            } else if (!espaco && saida.Length > 0) {
                saida.Append(' ');
                espaco = true;
            }
        }

        return saida.ToString().TrimEnd();
    }

    // Por palavra inteira: "war" nao pode estar contido em "warhammer".
    private static bool ContemPalavras(string texto, string trecho) =>
        $" {texto} ".Contains($" {trecho} ", StringComparison.Ordinal);

    private static double SobreposicaoPalavras(string alvo, string nome) {
        var palavrasAlvo = Palavras(alvo);
        var palavrasNome = Palavras(nome);
        if (palavrasAlvo.Count == 0 || palavrasNome.Count == 0) {
            return 0;
        }

        var comuns = palavrasAlvo.Intersect(palavrasNome).Count();
        return (double)comuns / Math.Max(palavrasAlvo.Count, palavrasNome.Count);
    }

    private static HashSet<string> Palavras(string texto) =>
        [.. texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !Irrelevantes.Contains(p))];

    private static double SemelhancaGrafia(string alvo, string nome) {
        var distancia = Levenshtein(alvo, nome);
        return 1 - (double)distancia / Math.Max(alvo.Length, nome.Length);
    }

    private static int Levenshtein(string a, string b) {
        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) {
            anterior[j] = j;
        }

        for (var i = 1; i <= a.Length; i++) {
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++) {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custo);
            }

            (anterior, atual) = (atual, anterior);
        }

        return anterior[b.Length];
    }
}
