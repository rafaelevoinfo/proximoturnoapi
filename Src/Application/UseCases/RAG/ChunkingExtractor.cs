using System.Text;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Um pedaço do manual pronto para virar embedding. Não carrega o jogo nem o link:
/// quem chama já tem esses ids no <see cref="ManualJob"/> e assim o chunking fica puro.
/// </summary>
public sealed record ManualChunk(int Ordem, string Titulo, string Texto) {

    /// <summary>
    /// O que realmente vai para o embedding. Sem o caminho de títulos um chunk isolado
    /// não diz de que regra ele fala, e "conte os pontos" some no meio de qualquer manual.
    /// </summary>
    public string TextoParaEmbedding => Titulo.Length == 0 ? Texto : $"{Titulo}\n\n{Texto}";
}

public interface IChunkingExtractor {
    Task<IReadOnlyList<ManualChunk>> ExtrairChunksAsync(string markdownFilePath, CancellationToken cancellationToken);
}

/// <summary>
/// Quebra o markdown do manual em chunks para indexação, seguindo os títulos.
/// Uma seção vira um chunk do tamanho que ela tem; só as grandes demais são partidas.
/// </summary>
public class ChunkingExtractor(ILogger<ChunkingExtractor> _logger) : IChunkingExtractor {

    // Contado em caracteres porque o projeto não tem tokenizador. Em pt-BR a razão
    // é de ~3,5 a 4 caracteres por token, entao 2000 caracteres ~ 540 tokens: cabe
    // uma regra inteira sem que tres regras sem relacao dividam o mesmo vetor.
    public const int TamanhoMaximo = 2000;

    // Abaixo disso a secao nao se sustenta sozinha ("## Fim do jogo" + duas linhas)
    // e e fundida na anterior, desde que o total continue dentro do teto.
    public const int TamanhoMinimo = 300;

    // Cauda repetida quando uma secao e partida, para que uma regra cortada no limite
    // nao se perca nas duas metades. So vale dentro da mesma secao: entre secoes
    // diferentes o overlap sujaria os dois embeddings.
    public const int Overlap = 200;

    private const string SeparadorTitulo = " > ";
    private const string SeparadorBloco = "\n\n";

    // Maior unidade indivisivel aceita. Garante que qualquer unidade ainda caiba
    // num pedaco que ja comece com o overlap herdado do anterior.
    private const int LimiteUnidade = TamanhoMaximo - Overlap - 2;

    /// <summary>Uma seção do markdown: o caminho de títulos, a linha de título crua e o corpo.</summary>
    private sealed record Secao(string Caminho, string LinhaTitulo, string Corpo);

    public async Task<IReadOnlyList<ManualChunk>> ExtrairChunksAsync(string markdownFilePath, CancellationToken cancellationToken) {
        var markdown = await File.ReadAllTextAsync(markdownFilePath, cancellationToken);
        var chunks = Dividir(markdown);

        _logger.LogInformation("Markdown {MarkdownFilePath} dividido em {Quantidade} chunks.", markdownFilePath, chunks.Count);
        return chunks;
    }

    /// <summary>
    /// Divisão propriamente dita. Isolada do I/O para poder ser testada sem tocar em arquivo.
    /// </summary>
    public static IReadOnlyList<ManualChunk> Dividir(string markdown) {
        if (string.IsNullOrWhiteSpace(markdown)) {
            return [];
        }

        var chunks = new List<ManualChunk>();
        foreach (var secao in Fundir(LerSecoes(markdown.Replace("\r\n", "\n")))) {
            foreach (var texto in DividirCorpo(secao.Corpo)) {
                chunks.Add(new ManualChunk(chunks.Count, secao.Caminho, texto));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Percorre o markdown montando o caminho de títulos ("Azul > Turno > Pegar peças").
    /// Título sem corpo não vira seção: o caminho dele já viaja nos filhos.
    /// </summary>
    private static List<Secao> LerSecoes(string markdown) {
        var secoes = new List<Secao>();
        var titulos = new List<string>();
        var niveis = new List<int>();
        var corpo = new StringBuilder();
        var caminho = "";
        var linhaTitulo = "";
        var dentroDeCerca = false;

        void Fechar() {
            var texto = corpo.ToString().Trim();
            if (texto.Length > 0) {
                secoes.Add(new Secao(caminho, linhaTitulo, texto));
            }
            corpo.Clear();
        }

        foreach (var bruta in markdown.Split('\n')) {
            var linha = bruta.TrimEnd('\r');

            if (linha.TrimStart().StartsWith("```")) {
                dentroDeCerca = !dentroDeCerca;
            }

            // Dentro de uma cerca, '#' e comentario de codigo, nao titulo.
            var nivel = dentroDeCerca ? 0 : NivelDoTitulo(linha);
            if (nivel == 0) {
                corpo.Append(linha).Append('\n');
                continue;
            }

            Fechar();

            // Um titulo de nivel N encerra tudo que estava aninhado abaixo dele.
            while (niveis.Count > 0 && niveis[^1] >= nivel) {
                niveis.RemoveAt(niveis.Count - 1);
                titulos.RemoveAt(titulos.Count - 1);
            }

            niveis.Add(nivel);
            titulos.Add(linha.TrimStart().TrimStart('#').Trim());
            caminho = string.Join(SeparadorTitulo, titulos);
            linhaTitulo = linha.Trim();
        }

        Fechar();
        return secoes;
    }

    /// <summary>
    /// Nível do título ATX (1 a 6), ou 0 se a linha não for título.
    /// "#texto" sem espaço não é título em markdown.
    /// </summary>
    private static int NivelDoTitulo(string linha) {
        var texto = linha.TrimStart();
        var nivel = 0;
        while (nivel < texto.Length && texto[nivel] == '#') {
            nivel++;
        }

        return nivel is > 0 and <= 6 && nivel < texto.Length && texto[nivel] == ' ' ? nivel : 0;
    }

    /// <summary>
    /// Funde seções curtas demais na anterior enquanto o total couber no teto.
    /// A linha de título da seção fundida é mantida no texto, senão os dois conteúdos
    /// se misturam sem nenhuma marca de onde um termina.
    /// </summary>
    private static List<Secao> Fundir(List<Secao> secoes) {
        var resultado = new List<Secao>();

        foreach (var secao in secoes) {
            if (resultado.Count > 0 && secao.Corpo.Length < TamanhoMinimo) {
                var anterior = resultado[^1];
                var juncao = secao.LinhaTitulo.Length > 0
                    ? anterior.Corpo + SeparadorBloco + secao.LinhaTitulo + SeparadorBloco + secao.Corpo
                    : anterior.Corpo + SeparadorBloco + secao.Corpo;

                if (juncao.Length <= TamanhoMaximo) {
                    resultado[^1] = anterior with { Corpo = juncao };
                    continue;
                }
            }

            resultado.Add(secao);
        }

        return resultado;
    }

    /// <summary>
    /// Empacota os blocos do corpo em pedaços dentro do teto, repetindo a cauda do
    /// anterior a cada quebra. Uma seção que já cabe sai inteira, num pedaço só.
    /// </summary>
    private static List<string> DividirCorpo(string corpo) {
        var pedacos = new List<string>();
        var atual = new StringBuilder();
        var temConteudo = false;

        void Fechar() {
            if (atual.Length > 0 && temConteudo) {
                pedacos.Add(atual.ToString().Trim());
            }
            atual.Clear();
            temConteudo = false;
        }

        void Adicionar(string unidade, string separador) {
            if (atual.Length > 0 && atual.Length + separador.Length + unidade.Length > TamanhoMaximo) {
                Fechar();
                if (pedacos.Count > 0) {
                    atual.Append(Cauda(pedacos[^1]));
                }
            }

            if (atual.Length > 0) {
                atual.Append(separador);
            }

            atual.Append(unidade);
            temConteudo = true;
        }

        foreach (var bloco in SepararBlocos(corpo)) {
            // Tabela grande e caso proprio: quebrar por frase destruiria as linhas
            // e os pedacos do meio ficariam sem cabecalho, ou seja, sem significado.
            if (bloco.Length > LimiteUnidade && EhTabela(bloco)) {
                Fechar();
                pedacos.AddRange(QuebrarTabela(bloco));
                continue;
            }

            // Dentro de um bloco de varias linhas (uma lista) a quebra natural e a linha;
            // num paragrafo corrido e a frase.
            var separadorInterno = bloco.Contains('\n') ? "\n" : " ";
            var unidades = bloco.Length <= LimiteUnidade ? [bloco] : QuebrarBloco(bloco);
            var primeira = true;

            foreach (var unidade in unidades) {
                Adicionar(unidade, primeira ? SeparadorBloco : separadorInterno);
                primeira = false;
            }
        }

        Fechar();
        return pedacos;
    }

    /// <summary>
    /// Separa o corpo em blocos por linha em branco. Uma cerca de código conta como
    /// um bloco só, mesmo tendo linhas em branco dentro.
    /// </summary>
    private static List<string> SepararBlocos(string corpo) {
        var blocos = new List<string>();
        var atual = new List<string>();
        var dentroDeCerca = false;

        foreach (var linha in corpo.Split('\n')) {
            if (linha.TrimStart().StartsWith("```")) {
                dentroDeCerca = !dentroDeCerca;
            }

            if (!dentroDeCerca && linha.Trim().Length == 0) {
                if (atual.Count > 0) {
                    blocos.Add(string.Join("\n", atual));
                    atual.Clear();
                }
                continue;
            }

            atual.Add(linha);
        }

        if (atual.Count > 0) {
            blocos.Add(string.Join("\n", atual));
        }

        return blocos;
    }

    /// <summary>
    /// Reduz um bloco grande demais a unidades que caibam: por linha quando há linhas,
    /// por frase dentro de cada linha, e corte seco só quando nem a frase cabe.
    /// </summary>
    private static List<string> QuebrarBloco(string bloco) {
        var unidades = new List<string>();
        var brutas = bloco.Contains('\n') ? bloco.Split('\n') : [.. QuebrarEmFrases(bloco)];

        foreach (var bruta in brutas) {
            var unidade = bruta.Trim();
            if (unidade.Length == 0) {
                continue;
            }

            if (unidade.Length <= LimiteUnidade) {
                unidades.Add(unidade);
                continue;
            }

            foreach (var frase in QuebrarEmFrases(unidade)) {
                unidades.AddRange(frase.Length <= LimiteUnidade ? [frase] : Fatiar(frase));
            }
        }

        return unidades;
    }

    /// <summary>
    /// Corta o texto no fim de cada frase. Um ponto só encerra frase quando vem
    /// espaço depois, para não partir "3.5" ou "Sr. Silva" ao meio.
    /// </summary>
    private static List<string> QuebrarEmFrases(string texto) {
        var frases = new List<string>();
        var inicio = 0;

        for (var i = 0; i < texto.Length; i++) {
            if (texto[i] is not ('.' or '!' or '?')) {
                continue;
            }

            if (i + 1 < texto.Length && !char.IsWhiteSpace(texto[i + 1])) {
                continue;
            }

            var frase = texto[inicio..(i + 1)].Trim();
            if (frase.Length > 0) {
                frases.Add(frase);
            }
            inicio = i + 1;
        }

        if (inicio < texto.Length) {
            var resto = texto[inicio..].Trim();
            if (resto.Length > 0) {
                frases.Add(resto);
            }
        }

        return frases;
    }

    /// <summary>
    /// Último recurso: corte seco no limite, recuando até o espaço mais próximo
    /// para pelo menos não partir uma palavra.
    /// </summary>
    private static List<string> Fatiar(string texto) {
        var fatias = new List<string>();

        for (var i = 0; i < texto.Length;) {
            var tamanho = Math.Min(LimiteUnidade, texto.Length - i);

            if (i + tamanho < texto.Length) {
                var espaco = texto.LastIndexOf(' ', i + tamanho - 1, tamanho);
                if (espaco > i) {
                    tamanho = espaco - i;
                }
            }

            var fatia = texto.Substring(i, tamanho).Trim();
            if (fatia.Length > 0) {
                fatias.Add(fatia);
            }

            i += tamanho;
        }

        return fatias;
    }

    private static bool EhTabela(string bloco) {
        var linhas = bloco.Split('\n');
        return linhas.Length >= 3 && linhas[0].TrimStart().StartsWith('|');
    }

    /// <summary>
    /// Quebra a tabela por linha, repetindo cabeçalho e separador em cada pedaço.
    /// Uma linha maior que o teto sozinha é mantida inteira: cortada ela não diria nada.
    /// </summary>
    private static List<string> QuebrarTabela(string bloco) {
        var linhas = bloco.Split('\n');
        var cabecalho = string.Join("\n", linhas.Take(2));
        var pedacos = new List<string>();
        var atual = new StringBuilder(cabecalho);

        foreach (var linha in linhas.Skip(2)) {
            if (atual.Length > cabecalho.Length && atual.Length + 1 + linha.Length > TamanhoMaximo) {
                pedacos.Add(atual.ToString());
                atual.Clear().Append(cabecalho);
            }

            atual.Append('\n').Append(linha);
        }

        if (atual.Length > cabecalho.Length) {
            pedacos.Add(atual.ToString());
        }

        return pedacos;
    }

    /// <summary>
    /// Cauda do pedaço anterior, começando na primeira palavra inteira,
    /// para o pedaço seguinte não abrir no meio de uma palavra.
    /// </summary>
    private static string Cauda(string pedaco) {
        if (pedaco.Length <= Overlap) {
            return pedaco;
        }

        var inicio = pedaco.Length - Overlap;
        var espaco = pedaco.IndexOfAny([' ', '\n'], inicio);
        if (espaco >= 0 && espaco + 1 < pedaco.Length) {
            inicio = espaco + 1;
        }

        return pedaco[inicio..].Trim();
    }
}
