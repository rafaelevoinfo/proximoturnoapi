using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using OpenAI;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.IA;

public class FabricaOpenRouter : IFabricaOpenRouter {

    private static readonly Uri Endereco = new("https://openrouter.ai/api/v1");

    // Embedding responde em segundos, e a chamada e curta e idempotente: repetir e barato.
    private static readonly TimeSpan TimeoutEmbedding = TimeSpan.FromMinutes(2);
    private const int TentativasEmbedding = 2;

    private readonly ILoggerFactory _loggerFactory;
    private readonly IRegistradorUsoLlm _registrador;
    private readonly Func<string?> _lerChave;

    private readonly ConcurrentDictionary<ChaveCliente, IChatClient> _chats = new();
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _embeddings = new();

    public FabricaOpenRouter(ILoggerFactory loggerFactory, IRegistradorUsoLlm registrador, Func<string?>? lerChave = null) {
        _loggerFactory = loggerFactory;
        _registrador = registrador;

        // Injetavel para o teste nao depender de variavel de ambiente do processo.
        _lerChave = lerChave ?? (() => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
    }

    /// <summary>
    /// O modelo entra na chave porque a policy de cada cliente carrega o modelo pedido — é
    /// assim que o ledger sabe o que foi pedido sem ninguém ler o corpo da requisição.
    /// </summary>
    private sealed record ChaveCliente(string Modelo, OperacaoLlm Operacao, TimeSpan Timeout, int Tentativas);

    public IChatClient CriarChat(string modelo, OperacaoLlm operacao, TimeSpan timeout, int tentativas) =>
        _chats.GetOrAdd(new ChaveCliente(modelo, operacao, timeout, tentativas),
                        chave => Cliente(chave.Modelo, chave.Operacao, chave.Timeout, chave.Tentativas)
                                 .GetChatClient(chave.Modelo).AsIChatClient());

    public IEmbeddingGenerator<string, Embedding<float>> CriarEmbedding(string modelo) =>
        _embeddings.GetOrAdd(modelo,
                             m => Cliente(m, OperacaoLlm.Embedding, TimeoutEmbedding, TentativasEmbedding)
                                  .GetEmbeddingClient(m).AsIEmbeddingGenerator());

    /// <summary>
    /// A chave é lida aqui, na primeira vez que alguém pede um cliente, e não no construtor:
    /// faltar chave precisa derrubar só a indexação, que roda em background e trata erro por
    /// manual, em vez de impedir a aplicação de subir.
    /// </summary>
    private OpenAIClient Cliente(string modelo, OperacaoLlm operacao, TimeSpan timeout, int tentativas) {
        var chave = _lerChave();
        if (string.IsNullOrWhiteSpace(chave)) {
            throw new InvalidOperationException("OPENROUTER_API_KEY não configurada.");
        }

        var opcoes = new OpenAIClientOptions() {
            Endpoint = Endereco,
            NetworkTimeout = timeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: tentativas),
        };

        // PerTry: cada tentativa vira uma linha, porque cada tentativa pode ter sido cobrada.
        opcoes.AddPolicy(new PoliticaUsoLlm(_loggerFactory.CreateLogger<PoliticaUsoLlm>(), _registrador, modelo, operacao),
                         PipelinePosition.PerTry);

        return new OpenAIClient(new ApiKeyCredential(chave), opcoes);
    }
}
