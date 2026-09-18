using ProximoTurnoApi.Application.UseCases.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RevisaoMarkdownTests {

    private static readonly ContextoManual Contexto = new("Balde de Caranguejo", "Modos extras");

    private static ResultadoBloco Aplicar(string bloco, params CorrecaoRevisao[] correcoes) =>
        RevisaoMarkdown.ValidarEAplicar(bloco, correcoes, bloco, Contexto);

    private static CorrecaoRevisao Correcao(string original, string corrigido) =>
        new(original, corrigido, "erro de leitura");

    [Fact]
    public void DividirEmBlocos_ConcatenacaoReproduzOOriginal() {
        var markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"# Seção {i}\n\nTexto da seção {i}. " + new string('x', 300)));

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        // Se a concatenacao nao bate, o revisor devolveria um documento com o espacamento
        // mexido: o chunking depende de linha em branco para separar blocos.
        Assert.True(blocos.Count > 1);
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void DividirEmBlocos_RespeitaOTamanhoQuandoOsParagrafosCabem() {
        var paragrafo = new string('a', 500);
        var markdown = string.Join("\n\n", Enumerable.Repeat(paragrafo, 20));

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        Assert.All(blocos, bloco => Assert.True(bloco.Length <= RevisaoMarkdown.TamanhoBloco + paragrafo.Length));
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void DividirEmBlocos_ParagrafoMaiorQueOLimite_VaiSozinhoEInteiro() {
        var gigante = new string('b', RevisaoMarkdown.TamanhoBloco * 2);
        var markdown = $"Curto.\n\n{gigante}\n\nOutro curto.";

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        // Cortar um paragrafo ao meio faria o revisor julgar um texto truncado.
        Assert.Contains(blocos, bloco => bloco.Contains(gigante));
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void ValidarEAplicar_ErroDeUmaLetra_Aplica() {
        var resultado = Aplicar("# MUDOS EXTRAS\n\nJogue.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("# MODOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Single(resultado.Aplicadas);
        Assert.Empty(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_AcentoTrocado_Aplica() {
        var resultado = Aplicar("# Modo Forrá\n\nJogado em times.", Correcao("Forrá", "Forró"));

        Assert.Contains("Modo Forró", resultado.Texto);
    }

    [Fact]
    public void ValidarEAplicar_TrechoQueNaoExisteNoBloco_Descarta() {
        var resultado = Aplicar("Texto sem erro.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("Texto sem erro.", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_MudarNumero_Descarta() {
        // Numero lido errado so o PDF resolve. Deixar um modelo de 8B "corrigir" numero
        // e trocar um erro de leitura por uma regra inventada.
        var resultado = Aplicar("Pegue 4 moedas.", Correcao("4 moedas", "6 moedas"));

        Assert.Equal("Pegue 4 moedas.", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_ReescritaLonga_Descarta() {
        var resultado = Aplicar("Modo Avexado é rápido.", Correcao("Avexado", "Apressado"));

        Assert.Contains("Avexado", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_PalavraCurta_Descarta() {
        var resultado = Aplicar("Carta de cor azul.", Correcao("de cor", "da cor"));

        Assert.Contains("de cor", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_PalavraDoNomeDoJogo_Descarta() {
        var resultado = Aplicar("Organize o Balde do jogo.", Correcao("Balde", "Balda"));

        Assert.Contains("Balde", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_TermoRepetidoNoDocumento_Descarta() {
        // Uso consistente e sinal de termo inventado pelo jogo, nao de erro de OCR.
        // A palavra precisa ser longa: uma curta como "Oxi!" ja seria barrada antes,
        // pela trava de minimo de letras, e o teste nao exercitaria esta regra.
        var documento = "Modo Avexado é rápido. No Avexado você grita. Vence quem sair do Avexado primeiro.";

        var resultado = RevisaoMarkdown.ValidarEAplicar(documento, [Correcao("Avexado", "Avexada")], documento, Contexto);

        Assert.Equal(documento, resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_SubstituiTodasAsOcorrenciasDoBloco() {
        var resultado = Aplicar("MUDOS de jogo. Os MUDOS são três.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("MODOS de jogo. Os MODOS são três.", resultado.Texto);
    }

    [Theory]
    [InlineData("MUDOS", "MODOS", 1)]
    [InlineData("Forrá", "Forró", 1)]
    [InlineData("Cornponentes", "Componentes", 2)]
    [InlineData("casa", "casa", 0)]
    public void Distancia_ContaOperacoesDeUmCaractere(string a, string b, int esperado) {
        Assert.Equal(esperado, RevisaoMarkdown.Distancia(a, b));
    }

    [Fact]
    public void Ocorrencias_ContaPalavraInteiraIgnorandoMaiusculas() {
        var documento = "Modo ligeiro. No MODO selvagem, o modojogo não conta.";

        Assert.Equal(2, RevisaoMarkdown.Ocorrencias(documento, "modo"));
    }
}
