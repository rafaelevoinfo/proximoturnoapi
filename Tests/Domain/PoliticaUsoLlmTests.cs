using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using ProximoTurnoApi.Infrastructure.IA;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PoliticaUsoLlmTests {

    private const string JsonOk = """
    {"id":"gen-teste-1","model":"deepseek/deepseek-v4-flash-20260423","provider":"Parasail",
     "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"ok"}}],
     "usage":{"prompt_tokens":120,"completion_tokens":30,"total_tokens":150,"cost":0.00001234,
              "prompt_tokens_details":{"cached_tokens":7},
              "completion_tokens_details":{"reasoning_tokens":11}}}
    """;

    private readonly FakeRegistradorUsoLlm _registrador = new();

    [Fact]
    public async Task RespostaCompleta_GravaTudoEOSdkAindaInterpreta() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk));

        var resposta = await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        Assert.Equal("ok", resposta.Text);
        Assert.Equal(120, resposta.Usage?.InputTokenCount);

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(OperacaoLlm.Ocr, linha.Operacao);
        Assert.Equal("modelo/pedido", linha.ModeloPedido);
        Assert.Equal("deepseek/deepseek-v4-flash-20260423", linha.ModeloRespondeu);
        Assert.Equal("Parasail", linha.Provider);
        Assert.Equal(120, linha.TokensEntrada);
        Assert.Equal(30, linha.TokensSaida);
        Assert.Equal(11, linha.TokensRaciocinio);
        Assert.Equal(7, linha.TokensCache);
        Assert.Equal(0.00001234m, linha.CustoUsd);
        Assert.Equal("gen-teste-1", linha.IdGeracao);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        Assert.Null(linha.Detalhe);
    }

    [Fact]
    public async Task FinishLength_GravaTruncadoComCusto() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk.Replace("\"stop\"", "\"length\"")));

        await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Truncado, linha.Desfecho);
        Assert.Equal(0.00001234m, linha.CustoUsd);
        Assert.Equal("length", linha.Detalhe);
    }

    // O finish_reason "error" da OpenRouter chega como HTTP 200 e o SDK so estoura depois, ao
    // desserializar um enum que nao conhece. A linha tem que sobreviver a isso.
    [Fact]
    public async Task FinishError_GravaComCustoAindaQueOSdkEstoure() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk.Replace("\"stop\"", "\"error\"")));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.ErroDoModelo, linha.Desfecho);
        Assert.Equal(0.00001234m, linha.CustoUsd);
        Assert.Equal("error", linha.Detalhe);
    }

    // Embedding nao tem choices, logo nao tem finish_reason: isso e resposta completa.
    [Fact]
    public async Task RespostaSemChoices_EhOkENaoErroDoModelo() {
        const string jsonEmbedding = """
        {"id":"gen-emb-1","model":"text-embedding-3-small","provider":"OpenAI",
         "usage":{"prompt_tokens":5,"total_tokens":5,"cost":0.0000001}}
        """;
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, jsonEmbedding));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        Assert.Equal(5, linha.TokensEntrada);
        Assert.Equal(0, linha.TokensSaida);
        Assert.Equal(0.0000001m, linha.CustoUsd);
    }

    [Fact]
    public async Task RespostaSemUsage_GravaLinhaComCustoNulo() {
        const string jsonSemUsage = """
        {"id":"gen-teste-2","model":"m","provider":"p",
         "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"ok"}}]}
        """;
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, jsonSemUsage));

        await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Null(linha.CustoUsd);
        Assert.Equal(0, linha.TokensEntrada);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
    }

    [Fact]
    public async Task CorpoNaoJson_GravaStatusEDuracaoSemNumeros() {
        var chat = Chat(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("<html>gateway</html>", Encoding.UTF8, "text/html")
        });

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Null(linha.CustoUsd);
        Assert.Equal(0, linha.TokensEntrada);
    }

    [Fact]
    public async Task Status429_GravaErroHttpComTrechoDoCorpo() {
        var chat = Chat(_ => Resposta(HttpStatusCode.TooManyRequests, """{"error":{"message":"rate limited"}}"""));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = _registrador.Registros[0];
        Assert.Equal(DesfechoLlm.ErroHttp, linha.Desfecho);
        Assert.Contains("429", linha.Detalhe);
        Assert.Contains("rate limited", linha.Detalhe);
    }

    [Fact]
    public async Task TransporteQueLanca_GravaExcecaoEARepassa() {
        var chat = Chat(_ => throw new HttpRequestException("rede caiu"));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Excecao, linha.Desfecho);
        Assert.Contains("rede caiu", linha.Detalhe);
        Assert.Null(linha.CustoUsd);
    }

    [Fact]
    public async Task RegistradorQueLanca_NaoDerrubaAChamada() {
        _registrador.Erro = new InvalidOperationException("ledger fora");
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk));

        var resposta = await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        Assert.Equal("ok", resposta.Text);
    }

    // PerTry: uma tentativa que morre do nosso lado pode ter sido cobrada, entao cada
    // tentativa precisa da sua linha.
    [Fact]
    public async Task DuasTentativas_DuasLinhas() {
        var chamadas = 0;
        var chat = Chat(_ => {
            chamadas++;
            return chamadas == 1
                ? Resposta(HttpStatusCode.InternalServerError, """{"error":{"message":"boom"}}""")
                : Resposta(HttpStatusCode.OK, JsonOk);
        }, tentativas: 1);

        await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        Assert.Equal(2, _registrador.Registros.Count);
        Assert.Equal(DesfechoLlm.ErroHttp, _registrador.Registros[0].Desfecho);
        Assert.Equal(DesfechoLlm.Ok, _registrador.Registros[1].Desfecho);
    }

    // Item 1 do Review Focus: a resposta do OCR carrega o manual inteiro no mesmo JSON.
    [Fact]
    public async Task RespostaGrande_LeOUsoSemEstourar() {
        var manual = new string('a', 400_000);
        var json = JsonOk.Replace("\"content\":\"ok\"", $"\"content\":\"{manual}\"");
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, json));

        var resposta = await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        Assert.Equal(400_000, resposta.Text?.Length);
        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(0.00001234m, linha.CustoUsd);
    }

    // Item 2 do Review Focus: custo real chega em notacao cientifica, e ha valores menores que
    // a precisao da coluna.
    [Theory]
    [InlineData("3.43e-07", 0.000000343)]
    [InlineData("1e-12", 0.0)]
    [InlineData("0", 0.0)]
    public async Task CustoEmNotacaoCientifica_EhLido(string bruto, double esperado) {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk.Replace("0.00001234", bruto)));

        await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        var linha = Assert.Single(_registrador.Registros);
        Assert.NotNull(linha.CustoUsd);
        Assert.Equal((decimal)esperado, Math.Round(linha.CustoUsd!.Value, 9));
    }

    // Item 3 do Review Focus: a policy e uma instancia so, compartilhada por chamadas
    // simultaneas. Estado por chamada guardado em campo misturaria as medicoes.
    [Fact]
    public async Task ChamadasSimultaneas_UmaLinhaCadaComOsSeusNumeros() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"))));

        Assert.Equal(8, _registrador.Registros.Count);
        Assert.All(_registrador.Registros, linha => {
            Assert.Equal(120, linha.TokensEntrada);
            Assert.Equal(0.00001234m, linha.CustoUsd);
            Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        });
    }

    private IChatClient Chat(Func<HttpRequestMessage, HttpResponseMessage> responder, int tentativas = 0) {
        var opcoes = new OpenAIClientOptions() {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(new HandlerFalso(responder))),
            RetryPolicy = new ClientRetryPolicy(maxRetries: tentativas),
        };

        opcoes.AddPolicy(new PoliticaUsoLlm(NullLogger<PoliticaUsoLlm>.Instance, _registrador,
                                            "modelo/pedido", OperacaoLlm.Ocr),
                         PipelinePosition.PerTry);

        return new OpenAIClient(new ApiKeyCredential("sk-falsa"), opcoes)
            .GetChatClient("modelo/pedido").AsIChatClient();
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> _responder) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
