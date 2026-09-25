using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Infrastructure.IA;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class FabricaOpenRouterTests {

    private static FabricaOpenRouter Fabrica(string? chave = "sk-falsa") =>
        new(NullLoggerFactory.Instance, new FakeRegistradorUsoLlm(), () => chave);

    [Fact]
    public void SemChave_Lanca() {
        var erro = Assert.Throws<InvalidOperationException>(
            () => Fabrica(chave: null).CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1));

        Assert.Equal("OPENROUTER_API_KEY não configurada.", erro.Message);
    }

    [Fact]
    public void ChaveEmBranco_Lanca() {
        Assert.Throws<InvalidOperationException>(
            () => Fabrica(chave: "   ").CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1));
    }

    // A chave nao pode ser lida no construtor: faltar chave precisa derrubar so a indexacao,
    // que roda em background, em vez de impedir a aplicacao de subir.
    [Fact]
    public void ChaveSoEhLidaQuandoAlguemPedeCliente() {
        var leituras = 0;
        var fabrica = new FabricaOpenRouter(NullLoggerFactory.Instance, new FakeRegistradorUsoLlm(), () => {
            leituras++;
            return "sk-falsa";
        });

        Assert.Equal(0, leituras);

        fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1);

        Assert.Equal(1, leituras);
    }

    [Fact]
    public void MesmaCombinacao_ReaproveitaOCliente() {
        var fabrica = Fabrica();

        var primeiro = fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1);
        var segundo = fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1);

        Assert.Same(primeiro, segundo);
    }

    [Fact]
    public void ModeloOuOperacaoDiferente_ClienteDiferente() {
        var fabrica = Fabrica();
        var referencia = fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1);

        Assert.NotSame(referencia, fabrica.CriarChat("outro", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 1));
        Assert.NotSame(referencia, fabrica.CriarChat("m", OperacaoLlm.RevisaoMarkdown, TimeSpan.FromMinutes(1), tentativas: 1));
        Assert.NotSame(referencia, fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(9), tentativas: 1));
        Assert.NotSame(referencia, fabrica.CriarChat("m", OperacaoLlm.Ocr, TimeSpan.FromMinutes(1), tentativas: 2));
    }

    [Fact]
    public void Embedding_MesmoModelo_ReaproveitaOGerador() {
        var fabrica = Fabrica();

        Assert.Same(fabrica.CriarEmbedding("openai/text-embedding-3-small"),
                    fabrica.CriarEmbedding("openai/text-embedding-3-small"));
    }
}
