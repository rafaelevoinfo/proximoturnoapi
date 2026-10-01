using System.Net;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;
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

    public Task<List<JogoChat>> ListarJogosAsync() => Task.FromResult(Jogos.ToList());

    public Task<JogoChat?> ObterJogoAsync(int idJogo) => Task.FromResult(Jogos.FirstOrDefault(j => j.Id == idJogo));
}

/// <summary>O modelo falso pede para chamar uma ferramenta, em vez de responder texto.</summary>
public sealed record Chamar(string Ferramenta, object Argumentos);

/// <summary>Rodada em que o modelo escreve algo e, na mesma resposta, pede uma ferramenta.</summary>
public sealed record TextoEChamar(string Texto, Chamar Chamar);

/// <summary>
/// Modelo falso com roteiro: cada chamada consome o próximo passo, que é um texto (resposta)
/// ou um <see cref="Chamar"/> (pedido de ferramenta, que o FunctionInvokingChatClient real
/// executa e devolve na chamada seguinte). Guarda o que recebeu e o alvo do ledger de cada
/// chamada.
/// </summary>
public sealed class FakeChatClient(params object[] passos) : IChatClient {

    public const string Falha = "<<falha>>";

    private readonly Queue<object> _passos = new(passos);
    private int _chamadas;

    public List<List<ChatMessage>> Recebidos { get; } = [];
    public List<ChatOptions?> Opcoes { get; } = [];
    public List<AlvoUsoLlm?> Alvos { get; } = [];

    private object Proximo(IEnumerable<ChatMessage> messages, ChatOptions? options) {
        Recebidos.Add([.. messages]);
        Opcoes.Add(options);
        Alvos.Add(EscopoUsoLlm.Atual);

        var passo = _passos.Count > 0 ? _passos.Dequeue() : "";
        if (passo is Falha) {
            throw new HttpRequestException("provedor fora do ar");
        }

        return passo;
    }

    private FunctionCallContent Chamada(Chamar chamar) {
        var json = System.Text.Json.JsonSerializer.Serialize(chamar.Argumentos);
        var argumentos = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        return new FunctionCallContent($"chamada-{++_chamadas}", chamar.Ferramenta, argumentos);
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
        var passo = Proximo(messages, options);
        var mensagem = passo switch {
            Chamar chamar => new ChatMessage(ChatRole.Assistant, [Chamada(chamar)]),
            TextoEChamar tc => new ChatMessage(ChatRole.Assistant, [new TextContent(tc.Texto), Chamada(tc.Chamar)]),
            _ => new ChatMessage(ChatRole.Assistant, (string)passo),
        };
        return Task.FromResult(new ChatResponse(mensagem));
    }

    /// <summary>Texto em pedaços de até 12 caracteres; pedido de ferramenta num pedaço só.</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await Task.Yield();
        var passo = Proximo(messages, options);

        if (passo is Chamar chamar) {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [Chamada(chamar)]);
            yield break;
        }

        if (passo is TextoEChamar textoEChamar) {
            yield return new ChatResponseUpdate(ChatRole.Assistant, textoEChamar.Texto);
            yield return new ChatResponseUpdate(ChatRole.Assistant, [Chamada(textoEChamar.Chamar)]);
            yield break;
        }

        var texto = (string)passo;
        for (var inicio = 0; inicio < texto.Length; inicio += 12) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, texto.Substring(inicio, Math.Min(12, texto.Length - inicio)));
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Guarda o que o caso de uso mandou para a tela, na ordem.</summary>
public sealed class FakeSaidaChat : ProximoTurnoApi.Application.UseCases.Chat.ISaidaChat {

    public List<ProximoTurnoApi.Application.DTOs.RespostaChatDTO> Cabecalhos { get; } = [];
    public List<string> Trechos { get; } = [];

    /// <summary>Executado a cada trecho escrito; serve para simular o usuário saindo no meio.</summary>
    public Action? AoEscrever { get; set; }

    public Task IniciarAsync(ProximoTurnoApi.Application.DTOs.RespostaChatDTO cabecalho, CancellationToken cancellationToken) {
        Cabecalhos.Add(cabecalho);
        return Task.CompletedTask;
    }

    /// <summary>Trechos e avisos de consultando, na ordem em que saíram.</summary>
    public List<string> Eventos { get; } = [];

    public const string Consultando = "<<consultando>>";

    public Task EscreverAsync(string trecho, CancellationToken cancellationToken) {
        Trechos.Add(trecho);
        Eventos.Add(trecho);
        AoEscrever?.Invoke();
        return Task.CompletedTask;
    }

    public Task ConsultandoAsync(CancellationToken cancellationToken) {
        Eventos.Add(Consultando);
        return Task.CompletedTask;
    }
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

/// <summary>Guarda as conversas em memória, como o banco guardaria, com o id gerado no insert.</summary>
public sealed class FakeChatConversaRepository : IChatConversaRepository {

    private int _proximoId = 1;

    public List<ChatConversa> Conversas { get; } = [];
    public List<ChatMensagem> Mensagens { get; } = [];
    public List<string> UsuariosExcluidos { get; } = [];

    public Task<ChatConversa?> ObterAsync(Guid chave, string idUsuario) {
        var conversa = Conversas.FirstOrDefault(c => c.Chave == chave && c.IdUsuario == idUsuario);
        // Copia, como o AsNoTracking: quem le nao pode mudar o que esta gravado.
        return Task.FromResult(conversa is null ? null : new ChatConversa {
            Id = conversa.Id, Chave = conversa.Chave, IdUsuario = conversa.IdUsuario, IdJogo = conversa.IdJogo,
            Sessao = conversa.Sessao, DataCriacao = conversa.DataCriacao, DataAtualizacao = conversa.DataAtualizacao,
        });
    }

    public Task SalvarAsync(ChatConversa conversa, ChatMensagem mensagem) {
        if (conversa.Id == 0) {
            conversa.Id = _proximoId++;
        }

        Conversas.RemoveAll(c => c.Id == conversa.Id);
        Conversas.Add(conversa);
        mensagem.IdConversa = conversa.Id;
        Mensagens.Add(mensagem);
        return Task.CompletedTask;
    }

    public Task ExcluirDoUsuarioAsync(string idUsuario) {
        UsuariosExcluidos.Add(idUsuario);
        Conversas.RemoveAll(c => c.IdUsuario == idUsuario);
        return Task.CompletedTask;
    }
}
