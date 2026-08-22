using ProximoTurnoApi.Infrastructure.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class PdfTextExtractorTests {

    private static PdfTextExtractor.ExtracaoManual Extracao(int confiabilidade) =>
        new("# Manual\n\nRegras.", confiabilidade);

    [Theory]
    // Confiabilidade ate 50: pula direto para o ultimo (melhor) modelo.
    [InlineData(0, 0, 2)]
    [InlineData(0, 40, 2)]
    [InlineData(0, 50, 2)]
    // Entre 51 e 80: escala apenas para o proximo da fila.
    [InlineData(0, 51, 1)]
    [InlineData(0, 80, 1)]
    // A partir do segundo modelo, o proximo ja e o ultimo nos dois casos.
    [InlineData(1, 30, 2)]
    [InlineData(1, 70, 2)]
    public void ProximoModelo_EscalonaConformeConfiabilidade(int indiceAtual, int confiabilidade, int esperado) {
        var proximo = PdfTextExtractor.ProximoModelo(indiceAtual, Extracao(confiabilidade), totalModelos: 3);

        Assert.Equal(esperado, proximo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(79)]
    public void ProximoModelo_NoUltimoModelo_NaoEscalonaMais(int confiabilidade) {
        var proximo = PdfTextExtractor.ProximoModelo(indiceAtual: 2, Extracao(confiabilidade), totalModelos: 3);

        Assert.Equal(2, proximo);
    }

    [Fact]
    public void ProximoModelo_ComUmUnicoModelo_NaoEscalona() {
        var proximo = PdfTextExtractor.ProximoModelo(indiceAtual: 0, Extracao(10), totalModelos: 1);

        Assert.Equal(0, proximo);
    }

    [Fact]
    public void ProximoModelo_SemExtracao_AvancaApenasUmDegrau() {
        // Falha total do modelo nao produz nota. Diferente de uma nota baixa, aqui nao ha
        // sinal de que o arquivo seja dificil, entao a cascata anda so um degrau.
        var proximo = PdfTextExtractor.ProximoModelo(indiceAtual: 0, extracao: null, totalModelos: 3);

        Assert.Equal(1, proximo);
    }

    [Fact]
    public void Interpretar_SentinelaNoFim_SeparaTextoENota() {
        var resposta = "# Azul\n\nRegras do jogo.\n\n<!--CONFIABILIDADE: 87-->";

        var extracao = PdfTextExtractor.Interpretar(resposta);

        Assert.NotNull(extracao);
        Assert.Equal("# Azul\n\nRegras do jogo.", extracao.Texto);
        Assert.Equal(87, extracao.Confiabilidade);
    }

    [Fact]
    public void Interpretar_SemSentinela_MantemTextoComNotaZero() {
        var extracao = PdfTextExtractor.Interpretar("# Azul\n\nRegras do jogo.");

        Assert.NotNull(extracao);
        Assert.Equal("# Azul\n\nRegras do jogo.", extracao.Texto);
        Assert.Equal(0, extracao.Confiabilidade);
    }

    [Fact]
    public void Interpretar_SentinelaDentroDeCercaDeCodigo_AindaEhLida() {
        // Alguns modelos envolvem a resposta inteira em uma cerca de codigo.
        var resposta = "```markdown\n# Azul\n\n<!--CONFIABILIDADE: 62-->\n```";

        var extracao = PdfTextExtractor.Interpretar(resposta);

        Assert.NotNull(extracao);
        Assert.Equal(62, extracao.Confiabilidade);
        Assert.DoesNotContain("CONFIABILIDADE", extracao.Texto);
    }

    [Fact]
    public void Interpretar_VariasSentinelas_UsaAUltima() {
        var resposta = "Exemplo de formato: <!--CONFIABILIDADE: 10-->\n\n# Azul\n\n<!--CONFIABILIDADE: 95-->";

        var extracao = PdfTextExtractor.Interpretar(resposta);

        Assert.NotNull(extracao);
        Assert.Equal(95, extracao.Confiabilidade);
    }

    [Theory]
    [InlineData("<!--CONFIABILIDADE: 999-->", 100)]
    [InlineData("<!--  CONFIABILIDADE:7  -->", 7)]
    public void Interpretar_NormalizaFormatoENota(string sentinela, int esperado) {
        var extracao = PdfTextExtractor.Interpretar("# Azul\n" + sentinela);

        Assert.NotNull(extracao);
        Assert.Equal(esperado, extracao.Confiabilidade);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<!--CONFIABILIDADE: 90-->")]
    public void Interpretar_SemTextoAproveitavel_DevolveNull(string? resposta) {
        Assert.Null(PdfTextExtractor.Interpretar(resposta));
    }
    [Fact]
    public void Interpretar_ManualGrande_EncontraSentinelaNaCauda() {
        var corpo = string.Join("\n", Enumerable.Repeat("Texto de regra do manual.", 8000));
        var resposta = corpo + "\n\n<!--CONFIABILIDADE: 73-->";

        var extracao = PdfTextExtractor.Interpretar(resposta);

        Assert.NotNull(extracao);
        Assert.Equal(73, extracao.Confiabilidade);
        Assert.EndsWith("Texto de regra do manual.", extracao.Texto);
    }

    [Fact]
    public void Interpretar_SentinelaLongeDaCauda_EhIgnorada() {
        // Nota solta no corpo do documento nao pode ser confundida com a do modelo.
        var corpo = string.Join("\n", Enumerable.Repeat("Texto de regra do manual.", 8000));
        var resposta = "<!--CONFIABILIDADE: 99-->\n" + corpo;

        var extracao = PdfTextExtractor.Interpretar(resposta);

        Assert.NotNull(extracao);
        Assert.Equal(0, extracao.Confiabilidade);
        Assert.Contains("CONFIABILIDADE: 99", extracao.Texto);
    }
}
