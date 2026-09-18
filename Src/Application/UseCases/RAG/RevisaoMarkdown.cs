using System.Text;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>Uma troca proposta pelo revisor.</summary>
public sealed record CorrecaoRevisao(string Original, string Corrigido, string Motivo);

/// <summary>Bloco depois de aplicadas as correções que passaram nas travas.</summary>
public sealed record ResultadoBloco(string Texto,
                                    List<CorrecaoRevisao> Aplicadas,
                                    List<(CorrecaoRevisao Correcao, string Motivo)> Descartadas);

/// <summary>
/// Parte determinística da revisão: corta o manual em blocos e decide o que pode entrar
/// no texto. O prompt não segura um modelo de 8B, então quem manda é este arquivo.
/// </summary>
public static class RevisaoMarkdown {

    // Blocos pequenos porque um modelo de 8B perde atencao em texto longo, e porque
    // alguns provedores do OpenRouter servem esse modelo com contexto curto.
    public const int TamanhoBloco = 4000;

    // Erro de OCR quase sempre custa uma ou duas letras. Acima disso o modelo esta
    // reescrevendo, e reescrita e exatamente o que nao queremos aqui.
    public const int DistanciaMaxima = 2;
    public const int MinimoDeLetras = 4;
    public const int MaximoDePalavras = 3;
    public const int RepeticoesParaTermoDoJogo = 3;

    private static readonly char[] SeparadoresDePalavra = [' ', '\n', '\r', '\t'];
    private static readonly char[] PontuacaoDasPontas = ['.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '*', '_', '-', '#', '>'];

    /// <summary>
    /// Corta o markdown em blocos de até <see cref="TamanhoBloco"/> caracteres, sempre em
    /// linha em branco. A concatenação dos blocos reproduz o original caractere a caractere.
    /// </summary>
    public static List<string> DividirEmBlocos(string markdown) {
        var blocos = new List<string>();
        if (string.IsNullOrEmpty(markdown)) {
            return blocos;
        }

        var atual = new StringBuilder();
        foreach (var paragrafo in SepararParagrafos(markdown)) {
            if (atual.Length > 0 && atual.Length + paragrafo.Length > TamanhoBloco) {
                blocos.Add(atual.ToString());
                atual.Clear();
            }

            atual.Append(paragrafo);
        }

        if (atual.Length > 0) {
            blocos.Add(atual.ToString());
        }

        return blocos;
    }

    /// <summary>
    /// Separa os parágrafos deixando a linha em branco colada no fim de cada um, para que
    /// juntar os pedaços de volta devolva o documento como ele era.
    /// </summary>
    private static IEnumerable<string> SepararParagrafos(string markdown) {
        var inicio = 0;
        var i = 0;

        while (i < markdown.Length) {
            if (markdown[i] != '\n') {
                i++;
                continue;
            }

            var fim = i + 1;
            var quebras = 1;
            while (fim < markdown.Length && (markdown[fim] is '\n' or '\r' or ' ' or '\t')) {
                if (markdown[fim] == '\n') {
                    quebras++;
                }
                fim++;
            }

            if (quebras >= 2) {
                yield return markdown[inicio..fim];
                inicio = fim;
            }

            i = fim;
        }

        if (inicio < markdown.Length) {
            yield return markdown[inicio..];
        }
    }

    /// <summary>
    /// Aplica no bloco as correções que passam nas travas. O documento inteiro vem junto
    /// porque uma das travas olha quantas vezes a palavra aparece no manual todo.
    /// </summary>
    public static ResultadoBloco ValidarEAplicar(string bloco,
                                                 IReadOnlyList<CorrecaoRevisao> correcoes,
                                                 string documento,
                                                 ContextoManual contexto) {
        var texto = bloco;
        var aplicadas = new List<CorrecaoRevisao>();
        var descartadas = new List<(CorrecaoRevisao, string)>();

        foreach (var correcao in correcoes) {
            var motivo = Barrar(texto, correcao, documento, contexto);
            if (motivo is not null) {
                descartadas.Add((correcao, motivo));
                continue;
            }

            texto = texto.Replace(correcao.Original, correcao.Corrigido, StringComparison.Ordinal);
            aplicadas.Add(correcao);
        }

        return new ResultadoBloco(texto, aplicadas, descartadas);
    }

    /// <summary>
    /// Diz por que a correção não pode entrar, ou null quando ela passa.
    /// </summary>
    private static string? Barrar(string bloco, CorrecaoRevisao correcao, string documento, ContextoManual contexto) {
        if (string.IsNullOrEmpty(correcao.Original) || !bloco.Contains(correcao.Original, StringComparison.Ordinal)) {
            return "trecho não encontrado no bloco";
        }

        if (Digitos(correcao.Original) != Digitos(correcao.Corrigido)) {
            return "mudaria um número";
        }

        var distancia = Distancia(correcao.Original, correcao.Corrigido);
        if (distancia is < 1 or > DistanciaMaxima) {
            return $"distância de edição {distancia}";
        }

        var originais = Palavras(correcao.Original);
        if (originais.Length > MaximoDePalavras) {
            return "trecho longo demais";
        }

        var corrigidas = Palavras(correcao.Corrigido);
        var alteradas = originais.Where(p => !corrigidas.Contains(p, StringComparer.Ordinal)).ToArray();
        if (alteradas.Length == 0) {
            return "nenhuma palavra muda";
        }

        foreach (var palavra in alteradas) {
            if (palavra.Count(char.IsLetter) < MinimoDeLetras) {
                return $"palavra curta demais: {palavra}";
            }

            if (EhTermoDoContexto(palavra, contexto)) {
                return $"palavra do nome do jogo ou do manual: {palavra}";
            }

            if (Ocorrencias(documento, palavra) >= RepeticoesParaTermoDoJogo) {
                return $"termo usado {RepeticoesParaTermoDoJogo}+ vezes no manual: {palavra}";
            }
        }

        return null;
    }

    /// <summary>
    /// Distância de edição: quantas inserções, remoções ou trocas de um caractere levam
    /// de um texto ao outro.
    /// </summary>
    public static int Distancia(string a, string b) {
        if (a.Length == 0) {
            return b.Length;
        }

        if (b.Length == 0) {
            return a.Length;
        }

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

    /// <summary>Quantas vezes a palavra aparece inteira no documento, ignorando maiúsculas.</summary>
    public static int Ocorrencias(string documento, string palavra) {
        if (palavra.Length == 0) {
            return 0;
        }

        var total = 0;
        var indice = documento.IndexOf(palavra, StringComparison.OrdinalIgnoreCase);

        while (indice >= 0) {
            var fim = indice + palavra.Length;
            var antes = indice == 0 || !char.IsLetterOrDigit(documento[indice - 1]);
            var depois = fim >= documento.Length || !char.IsLetterOrDigit(documento[fim]);

            if (antes && depois) {
                total++;
            }

            indice = indice + 1 < documento.Length
                ? documento.IndexOf(palavra, indice + 1, StringComparison.OrdinalIgnoreCase)
                : -1;
        }

        return total;
    }

    private static string Digitos(string texto) => new([.. texto.Where(char.IsDigit)]);

    private static string[] Palavras(string texto) =>
        [.. texto.Split(SeparadoresDePalavra, StringSplitOptions.RemoveEmptyEntries)
                 .Select(p => p.Trim(PontuacaoDasPontas))
                 .Where(p => p.Length > 0)];

    private static bool EhTermoDoContexto(string palavra, ContextoManual contexto) =>
        Palavras($"{contexto.NomeJogo} {contexto.TituloManual}")
            .Where(p => p.Count(char.IsLetter) >= 3)
            .Any(p => string.Equals(p, palavra, StringComparison.OrdinalIgnoreCase));
}
