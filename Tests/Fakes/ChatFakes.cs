using System.Net;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Fakes;

public sealed class FakeCotacaoDolar(decimal valor = 5m) : ICotacaoDolar {

    public decimal Valor { get; set; } = valor;

    public CotacaoUsdBrl Atual => new(Valor, FonteCotacao.Api, DateTime.Now);

    public Task<CotacaoUsdBrl> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(Atual);
}

public sealed class FakeChatRegrasRepository : IChatRegrasRepository {

    public decimal ValorBaseAlugado { get; set; }
    public decimal Gasto { get; set; }
    public List<JogoChat> Jogos { get; } = [];

    public Task<decimal> SomarValorBaseAlugadoAsync(string email) => Task.FromResult(ValorBaseAlugado);

    public Task<decimal> SomarGastoAsync(string idUsuario) => Task.FromResult(Gasto);

    public Task<List<JogoChat>> ListarJogosComManualAsync() => Task.FromResult(Jogos.Where(j => j.TemManual).ToList());

    public Task<JogoChat?> ObterJogoAsync(int idJogo) => Task.FromResult(Jogos.FirstOrDefault(j => j.Id == idJogo));
}

/// <summary>
/// Devolve uma resposta pronta por chamada, na ordem configurada, e guarda o que recebeu e o
/// alvo do ledger visto no momento da chamada.
/// </summary>
public sealed class FakeChatClient(params string[] respostas) : IChatClient {

    public const string Falha = "<<falha>>";

    private readonly Queue<string> _respostas = new(respostas);

    public List<List<ChatMessage>> Recebidos { get; } = [];
    public List<ChatOptions?> Opcoes { get; } = [];
    public List<AlvoUsoLlm?> Alvos { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
        Recebidos.Add([.. messages]);
        Opcoes.Add(options);
        Alvos.Add(EscopoUsoLlm.Atual);

        var resposta = _respostas.Count > 0 ? _respostas.Dequeue() : "";
        if (resposta == Falha) {
            throw new HttpRequestException("provedor fora do ar");
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, resposta)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>> {

    public List<string> Textos { get; } = [];
    public List<AlvoUsoLlm?> Alvos { get; } = [];

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
                                                                     CancellationToken cancellationToken = default) {
        var entradas = values.ToList();
        Textos.AddRange(entradas);
        Alvos.Add(EscopoUsoLlm.Atual);

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
            entradas.Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f }))));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Responde com status e corpo configurados, ou lança; conta as chamadas.</summary>
public sealed class FakeCotacaoHandler : HttpMessageHandler {

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Corpo { get; set; } = "";
    public Exception? Erro { get; set; }
    public int Chamadas { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Chamadas++;
        if (Erro is not null) {
            throw Erro;
        }

        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Corpo) });
    }
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory {

    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
