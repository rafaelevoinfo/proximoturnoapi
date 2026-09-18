using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class LlmMarkdownRevisorTests {

    private const string Falha = "__falha__";
    private static readonly ContextoManual Contexto = new("Balde de Caranguejo", "Modos extras");

    /// <summary>Devolve uma resposta pronta por chamada, na ordem em que foram configuradas.</summary>
    private sealed class ChatFalso(params string[] respostas) : IChatClient {
        private readonly Queue<string> _respostas = new(respostas);

        public List<string> Recebidos { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Recebidos.Add(string.Join("\n", messages.Select(m => m.Text)));

            var resposta = _respostas.Count > 0 ? _respostas.Dequeue() : "{\"correcoes\":[]}";
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

    private static LlmMarkdownRevisor Revisor(ChatFalso chat) =>
        new(NullLogger<LlmMarkdownRevisor>.Instance, chat);

    [Fact]
    public async Task RevisarAsync_AplicaCorrecaoDoModelo() {
        var chat = new ChatFalso("{\"correcoes\":[{\"original\":\"MUDOS\",\"corrigido\":\"MODOS\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MODOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Equal(1, resultado.Aplicadas);
        Assert.Equal(0, resultado.Descartadas);
        Assert.True(resultado.Completa);
        Assert.NotNull(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_CorrecaoBarradaPelasTravas_ContaComoDescartada() {
        var chat = new ChatFalso("{\"correcoes\":[{\"original\":\"4 moedas\",\"corrigido\":\"6 moedas\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync("Pegue 4 moedas.", Contexto, CancellationToken.None);

        Assert.Equal("Pegue 4 moedas.", resultado.Texto);
        Assert.Equal(0, resultado.Aplicadas);
        Assert.Equal(1, resultado.Descartadas);
    }

    [Fact]
    public async Task RevisarAsync_JsonComTextoEmVolta_AindaEhLido() {
        var chat = new ChatFalso("Claro! Aqui está:\n```json\n{\"correcoes\":[{\"original\":\"Forrá\",\"corrigido\":\"Forró\",\"motivo\":\"leitura\"}]}\n```");

        var resultado = await Revisor(chat).RevisarAsync("# Modo Forrá\n\nEm times.", Contexto, CancellationToken.None);

        Assert.Contains("Modo Forró", resultado.Texto);
        Assert.True(resultado.Completa);
    }

    [Fact]
    public async Task RevisarAsync_RespostaInvalida_MantemOBlocoEMarcaIncompleta() {
        var chat = new ChatFalso("desculpe, não entendi");

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MUDOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.False(resultado.Completa);
        Assert.Null(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_UmBlocoFalhaEOutroNao_RevisaOQueDeuEMarcaIncompleta() {
        var grande = new string('a', RevisaoMarkdown.TamanhoBloco);
        var markdown = $"{grande}\n\n# MUDOS EXTRAS\n\nJogue.";
        var chat = new ChatFalso(Falha, "{\"correcoes\":[{\"original\":\"MUDOS\",\"corrigido\":\"MODOS\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync(markdown, Contexto, CancellationToken.None);

        Assert.Equal(2, chat.Recebidos.Count);
        Assert.Contains("MODOS EXTRAS", resultado.Texto);
        Assert.StartsWith(grande, resultado.Texto);
        Assert.False(resultado.Completa);
        // Um bloco revisado ja vale cache: o texto parcial e melhor que o do OCR cru.
        Assert.NotNull(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_TodosOsBlocosFalham_DevolveOOriginalSemModelo() {
        var chat = new ChatFalso(Falha);

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MUDOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Null(resultado.Modelo);
        Assert.False(resultado.Completa);
    }

    [Fact]
    public async Task RevisarAsync_ListaVazia_ContaComoRevisadoSemMudarNada() {
        var chat = new ChatFalso("{\"correcoes\":[]}");

        var resultado = await Revisor(chat).RevisarAsync("Texto correto.", Contexto, CancellationToken.None);

        Assert.Equal("Texto correto.", resultado.Texto);
        Assert.True(resultado.Completa);
        Assert.NotNull(resultado.Modelo);
    }
}
