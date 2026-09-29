using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.IA;
using Microsoft.Extensions.Logging;
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

    // Um campo de tipo inesperado dentro do usage nao pode derrubar a linha inteira: tokens,
    // modelo, provider e id continuam valendo mesmo sem o custo.
    [Theory]
    [InlineData("null")]
    [InlineData("\"0.0000343\"")]
    public async Task CustoComTipoInesperado_GravaORestoDaLinha(string custoBruto) {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk.Replace("0.00001234", custoBruto)));

        await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Null(linha.CustoUsd);
        Assert.Equal(120, linha.TokensEntrada);
        Assert.Equal(30, linha.TokensSaida);
        Assert.Equal("deepseek/deepseek-v4-flash-20260423", linha.ModeloRespondeu);
        Assert.Equal("Parasail", linha.Provider);
        Assert.Equal("gen-teste-1", linha.IdGeracao);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
    }

    // Aqui a policy fica mais robusta que o SDK: ele lanca em ChatTokenUsage ao ler
    // prompt_tokens nulo, e a linha do ledger tem que sobreviver a isso.
    [Fact]
    public async Task TokensComTipoInesperado_GravaORestoDaLinha() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk.Replace("\"prompt_tokens\":120", "\"prompt_tokens\":null")));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(0, linha.TokensEntrada);
        Assert.Equal(30, linha.TokensSaida);
        Assert.Equal(0.00001234m, linha.CustoUsd);
        Assert.Equal("Parasail", linha.Provider);
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

    // O caso que justifica o PerTry: o timeout e NOSSO, o servidor segue e cobra. Sem linha,
    // esse gasto fica invisivel - que e exatamente o problema que o ledger existe para matar.
    [Fact]
    public async Task TimeoutDoCliente_GravaExcecao() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk), atrasoMs: 3000,
                        timeout: TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Excecao, linha.Desfecho);
        Assert.Contains("Cancel", linha.Detalhe);
        Assert.Null(linha.CustoUsd);
    }

    [Fact]
    public async Task TimeoutComRetentativa_UmaLinhaPorTentativa() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk), tentativas: 2, atrasoMs: 3000,
                        timeout: TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<Exception>(() => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi")));

        Assert.Equal(3, _registrador.Registros.Count);
        Assert.All(_registrador.Registros, linha => Assert.Equal(DesfechoLlm.Excecao, linha.Desfecho));
    }

    // Desligamento e o ponto cego que o spec assume: o chamador cancelou, nao ha resposta para
    // ler e o DbContext ja pode estar indo embora.
    [Fact]
    public async Task CancelamentoDoChamador_NaoGravaLinha() {
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk), atrasoMs: 3000);
        using var fonte = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"), cancellationToken: fonte.Token));

        Assert.Empty(_registrador.Registros);
    }

    [Fact]
    public async Task RegistradorQueLanca_NaoDerrubaAChamada() {
        _registrador.Erro = new InvalidOperationException("ledger fora");
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk));

        var resposta = await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"));

        Assert.Equal("ok", resposta.Text);
    }

    // "A policy nunca lanca" precisa ser propriedade do codigo, nao de uma auditoria: um sink
    // de log defeituoso nao pode transformar uma chamada paga e concluida em chamada perdida.
    [Fact]
    public async Task RegistradorELoggerQueLancam_NaoDerrubamAChamada() {
        _registrador.Erro = new InvalidOperationException("ledger fora");
        var chat = Chat(_ => Resposta(HttpStatusCode.OK, JsonOk), logger: new LoggerQueLanca());

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
    // simultaneas. Cada resposta traz numeros proprios: se a policy guardasse medicao em campo,
    // as linhas sairiam com numeros trocados ou repetidos.
    [Fact]
    public async Task ChamadasSimultaneas_CadaLinhaComOsSeusNumeros() {
        var proxima = 0;
        var chat = Chat(_ => {
            var indice = Interlocked.Increment(ref proxima) - 1;
            return Resposta(HttpStatusCode.OK, JsonOk
                .Replace("\"prompt_tokens\":120", $"\"prompt_tokens\":{1000 + indice}")
                .Replace("gen-teste-1", $"gen-teste-{indice}"));
        });

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            await chat.GetResponseAsync(new ChatMessage(ChatRole.User, "oi"))));

        Assert.Equal(8, _registrador.Registros.Count);
        Assert.Equal(Enumerable.Range(1000, 8), _registrador.Registros.Select(l => l.TokensEntrada).OrderBy(t => t));
        Assert.Equal(8, _registrador.Registros.Select(l => l.IdGeracao).Distinct().Count());
    }

    private IChatClient Chat(Func<HttpRequestMessage, HttpResponseMessage> responder,
                             int tentativas = 0,
                             int atrasoMs = 0,
                             TimeSpan? timeout = null,
                             ILogger<PoliticaUsoLlm>? logger = null) {
        var opcoes = new OpenAIClientOptions() {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(new HandlerFalso(responder, atrasoMs))),
            RetryPolicy = new ClientRetryPolicy(maxRetries: tentativas),
        };

        if (timeout is not null) {
            opcoes.NetworkTimeout = timeout.Value;
        }

        opcoes.AddPolicy(new PoliticaUsoLlm(logger ?? NullLogger<PoliticaUsoLlm>.Instance, _registrador,
                                            "modelo/pedido", OperacaoLlm.Ocr),
                         PipelinePosition.PerTry);

        return new OpenAIClient(new ApiKeyCredential("sk-falsa"), opcoes)
            .GetChatClient("modelo/pedido").AsIChatClient();
    }

    // Streaming como a OpenRouter manda: texto em pedacos, finish_reason no penultimo e usage
    // (com custo) no ultimo, antes do [DONE].
    private const string SseOk =
        "data: {\"id\":\"gen-sse-1\",\"model\":\"deepseek/deepseek-v4-flash-20260423\",\"provider\":\"Parasail\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Tro\"},\"finish_reason\":null}]}\n\n" +
        ": OPENROUTER PROCESSING\n\n" +
        "data: {\"id\":\"gen-sse-1\",\"model\":\"deepseek/deepseek-v4-flash-20260423\",\"provider\":\"Parasail\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"que 4:1\"},\"finish_reason\":\"stop\"}]}\n\n" +
        "data: {\"id\":\"gen-sse-1\",\"model\":\"deepseek/deepseek-v4-flash-20260423\",\"provider\":\"Parasail\",\"choices\":[],\"usage\":{\"prompt_tokens\":900,\"completion_tokens\":42,\"cost\":0.0000512,\"prompt_tokens_details\":{\"cached_tokens\":3},\"completion_tokens_details\":{\"reasoning_tokens\":0}}}\n\n" +
        "data: [DONE]\n\n";

    private static HttpResponseMessage RespostaSse(string corpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "text/event-stream") };

    [Fact]
    public async Task Streaming_UsuarioRecebeOTextoELedgerGravaCustoDoUltimoPedaco() {
        var chat = Chat(_ => RespostaSse(SseOk));

        var texto = new StringBuilder();
        await foreach (var pedaco in chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi"))) {
            texto.Append(pedaco.Text);
        }

        Assert.Equal("Troque 4:1", texto.ToString());
        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal("deepseek/deepseek-v4-flash-20260423", linha.ModeloRespondeu);
        Assert.Equal("Parasail", linha.Provider);
        Assert.Equal(900, linha.TokensEntrada);
        Assert.Equal(42, linha.TokensSaida);
        Assert.Equal(3, linha.TokensCache);
        Assert.Equal(0.0000512m, linha.CustoUsd);
        Assert.Equal("gen-sse-1", linha.IdGeracao);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        Assert.Null(linha.Detalhe);
    }

    // O gasto de uma resposta em streaming so e conhecido no fim, quando quem le pode estar em
    // outro escopo. A linha tem que sair com o alvo de quando a chamada foi feita.
    [Fact]
    public async Task Streaming_LinhaLevaOAlvoDeQuandoAChamadaSaiu() {
        var chat = Chat(_ => RespostaSse(SseOk));
        IAsyncEnumerator<ChatResponseUpdate> leitor;

        using (EscopoUsoLlm.Abrir(17, null, "Chat de regras / Catan", "usuario-1")) {
            leitor = chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi")).GetAsyncEnumerator();
            await leitor.MoveNextAsync();
        }

        while (await leitor.MoveNextAsync()) { }
        await leitor.DisposeAsync();

        Assert.Single(_registrador.Registros);
        Assert.Equal("usuario-1", _registrador.Alvos.Single()?.IdUsuario);
        Assert.Equal(17, _registrador.Alvos.Single()?.IdJogo);
    }

    // Conexao que cai no meio: o corpo termina sem usage e sem [DONE]. O servidor pode ter
    // cobrado, mas nao disse quanto.
    [Fact]
    public async Task Streaming_InterrompidoAntesDoFim_GravaComoExcecao() {
        var cortado = SseOk[..SseOk.IndexOf(": OPENROUTER", StringComparison.Ordinal)];
        var chat = Chat(_ => RespostaSse(cortado));

        await foreach (var _ in chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi"))) { }

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Excecao, linha.Desfecho);
        Assert.Equal("Streaming interrompido antes do fim.", linha.Detalhe);
        Assert.Equal("gen-sse-1", linha.IdGeracao);
    }

    [Fact]
    public async Task Streaming_FinishLength_GravaTruncado() {
        var chat = Chat(_ => RespostaSse(SseOk.Replace("\"finish_reason\":\"stop\"", "\"finish_reason\":\"length\"")));

        await foreach (var _ in chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi"))) { }

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Truncado, linha.Desfecho);
        Assert.Equal("length", linha.Detalhe);
        Assert.Equal(0.0000512m, linha.CustoUsd);
    }

    // Sem include_usage o provedor pode omitir o usage do streaming, e a chamada sairia sem custo.
    [Fact]
    public async Task Streaming_PedeOUsoNaRequisicao() {
        string? corpo = null;
        var chat = Chat(requisicao => {
            corpo = requisicao.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return RespostaSse(SseOk);
        });

        await foreach (var _ in chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi"))) { }

        Assert.Contains("\"include_usage\":true", corpo);
    }

    // Pedido de ferramenta nao e erro: e o modelo usando listar_jogos ou buscar_regras.
    [Fact]
    public async Task Streaming_FinishToolCalls_GravaOk() {
        var chat = Chat(_ => RespostaSse(SseOk.Replace("\"finish_reason\":\"stop\"", "\"finish_reason\":\"tool_calls\"")));

        await foreach (var _ in chat.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "oi"))) { }

        var linha = Assert.Single(_registrador.Registros);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        Assert.Null(linha.Detalhe);
    }

    [Fact]
    public void UsoSse_LinhaQuebradaEComentario_NaoLancam() {
        var uso = new UsoSse();

        uso.LerLinha(": OPENROUTER PROCESSING");
        uso.LerLinha("data: {quebrado");
        uso.LerLinha("event: qualquer");
        uso.LerLinha("data: [DONE]");

        Assert.True(uso.Concluido);
        Assert.False(uso.TemUso);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> _responder, int _atrasoMs = 0)
        : HttpMessageHandler {

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (_atrasoMs > 0) {
                await Task.Delay(_atrasoMs, cancellationToken);
            }

            return _responder(request);
        }
    }

    /// <summary>Sink defeituoso: o Logger junta a falha dos providers e relança.</summary>
    private sealed class LoggerQueLanca : ILogger<PoliticaUsoLlm> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new AggregateException(new InvalidOperationException("sink quebrado"));
    }
}
