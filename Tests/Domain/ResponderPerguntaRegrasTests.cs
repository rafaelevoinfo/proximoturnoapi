using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ResponderPerguntaRegrasTests {

    private static readonly UsuarioChat Cliente = new("usuario-1", "cliente@teste.com", Admin: false);
    private static readonly UsuarioChat Admin = new("admin-1", "admin@teste.com", Admin: true);

    private readonly FakeChatRegrasRepository _repositorio = new();
    private readonly FakeManualVectorStore _vetores = new();
    private readonly FakeEmbeddingGenerator _embedding = new();
    private FakeChatClient _redator = new("Na sua vez, você pode trocar 4 recursos iguais com o banco.");

    public ResponderPerguntaRegrasTests() {
        _repositorio.Jogos.AddRange([
            new JogoChat(1, "Catan", true),
            new JogoChat(2, "Catan: Cidades e Cavaleiros", true),
            new JogoChat(3, "Ticket to Ride", true),
            new JogoChat(9, "Jogo Sem Manual", false),
        ]);
        _vetores.Trechos.AddRange([
            new TrechoManual(1, 10, "Catan > Comércio", "Troca 4:1 com o banco.", 0.9f),
            new TrechoManual(3, 30, "Ticket > Trens", "Compre cartas de trem.", 0.9f),
        ]);
    }

    private ResponderPerguntaRegras Caso() =>
        new(NullLogger<ResponderPerguntaRegras>.Instance,
            new ObterSaldoChat(_repositorio, new FakeCotacaoDolar(5m), ConfiguracaoChat.Padrao),
            _repositorio, _vetores, _redator, _embedding);

    private static PerguntaChatDTO Pergunta(string mensagem, int? confirmado = null, int? pagina = null) =>
        new() { Mensagem = mensagem, IdJogoConfirmado = confirmado, IdJogoPagina = pagina };

    [Fact]
    public async Task AbertoNaPaginaDoJogo_RespondeSemPerguntarOJogo() {
        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        Assert.Equal(TipoRespostaChat.Resposta, resposta!.Tipo);
        Assert.Equal(new JogoChatDTO(1, "Catan"), resposta.Jogo);
        Assert.Equal([1], _vetores.JogosBuscados);
        Assert.Equal([new FonteChatDTO(10, "Catan > Comércio")], resposta.Fontes);
    }

    // Pedido do produto: nada de chamada so para classificar a pergunta.
    [Fact]
    public async Task UmaPergunta_UmaChamadaDeChatSo() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        Assert.Single(_redator.Recebidos);
    }

    [Fact]
    public async Task InstrucoesTrazemAsRegrasDoAssistente() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        var instrucoes = _redator.Opcoes.Single()!.Instructions!;
        Assert.Contains("SOMENTE dúvidas sobre as regras do jogo Catan", instrucoes);
        Assert.Contains("[[OUTRO_JOGO:nome do jogo]]", instrucoes);
        Assert.Contains("recuse", instrucoes);
        Assert.Contains("Troca 4:1 com o banco.", instrucoes);
    }

    [Fact]
    public async Task SemJogoNemCitacao_PerguntaQualJogoSemGastar() {
        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Como faz para pontuar?"));

        Assert.Equal(TipoRespostaChat.PerguntarJogo, resposta!.Tipo);
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_redator.Recebidos);
    }

    [Fact]
    public async Task SemJogoComCitacao_PedeConfirmacaoSemGastar() {
        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("No catan posso trocar com o banco?"));

        Assert.Equal(TipoRespostaChat.ConfirmarJogo, resposta!.Tipo);
        Assert.Equal([1, 2], resposta.OpcoesJogo.Select(j => j.Id));
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_redator.Recebidos);
    }

    [Fact]
    public async Task ModeloApontaOutroJogo_PedeConfirmacao() {
        _redator = new FakeChatClient("[[OUTRO_JOGO: Ticket to Ride]]");

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("E no Ticket to Ride, como compro cartas?", pagina: 1));

        Assert.Equal(TipoRespostaChat.ConfirmarJogo, resposta!.Tipo);
        Assert.Equal([3], resposta.OpcoesJogo.Select(j => j.Id));
        Assert.Equal(new JogoChatDTO(1, "Catan"), resposta.Jogo);
    }

    [Fact]
    public async Task ModeloApontaJogoForaDoCatalogo_AvisaEMantemOJogo() {
        _redator = new FakeChatClient("[[OUTRO_JOGO:Monopoly]]");

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("E no Monopoly?", pagina: 1));

        Assert.Equal(TipoRespostaChat.SemManual, resposta!.Tipo);
        Assert.Equal(new JogoChatDTO(1, "Catan"), resposta.Jogo);
    }

    [Fact]
    public async Task JogoConfirmadoVenceODaPagina() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Como compro cartas?", confirmado: 3, pagina: 1));

        Assert.Equal([3], _vetores.JogosBuscados);
    }

    [Fact]
    public async Task BuscaNuncaTrazTrechoDeOutroJogo() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Como compro cartas?", confirmado: 3));

        var instrucoes = _redator.Opcoes.Single()!.Instructions!;
        Assert.Contains("Compre cartas de trem.", instrucoes);
        Assert.DoesNotContain("Troca 4:1", instrucoes);
    }

    [Fact]
    public async Task SaldoEsgotado_MensagemAmigavelSemChamarLlmNenhum() {
        _repositorio.Gasto = 100m;

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        Assert.Equal(TipoRespostaChat.SaldoEsgotado, resposta!.Tipo);
        Assert.Equal(ResponderPerguntaRegras.MensagemSaldoEsgotado, resposta.Texto);
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_redator.Recebidos);
    }

    [Fact]
    public async Task Admin_SemLimiteDeSaldo() {
        _repositorio.Gasto = 100m;

        var resposta = await Caso().ExecuteAsync(Admin, Pergunta("Posso trocar com o banco?", pagina: 1));

        Assert.Equal(TipoRespostaChat.Resposta, resposta!.Tipo);
    }

    [Fact]
    public async Task JogoSemManual_AvisaSemGastar() {
        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Como joga?", pagina: 9));

        Assert.Equal(TipoRespostaChat.SemManual, resposta!.Tipo);
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_redator.Recebidos);
    }

    // Sem trecho, o modelo ainda decide: pode ser cumprimento, outro assunto ou outro jogo.
    [Fact]
    public async Task ManualSemTrechoParecido_ModeloAindaResponde() {
        _vetores.Trechos.Clear();

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Oi!", pagina: 1));

        Assert.Equal(TipoRespostaChat.Resposta, resposta!.Tipo);
        Assert.Contains("nenhum trecho", _redator.Opcoes.Single()!.Instructions!);
    }

    // O gasto de cada etapa precisa ir para o credito de quem perguntou, no jogo certo.
    [Fact]
    public async Task TodasAsChamadasPagasCarregamOUsuarioEOJogo() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        var alvos = _embedding.Alvos.Concat(_redator.Alvos).ToList();
        Assert.Equal(2, alvos.Count);
        Assert.All(alvos, alvo => {
            Assert.Equal("usuario-1", alvo?.IdUsuario);
            Assert.Equal(1, alvo?.IdJogo);
        });
    }

    [Fact]
    public async Task PerguntaVazia_EhInvalida() {
        var caso = Caso();

        var resposta = await caso.ExecuteAsync(Cliente, Pergunta("   ", pagina: 1));

        Assert.Null(resposta);
        Assert.False(caso.IsValid);
    }

    [Fact]
    public async Task PerguntaLongaDemais_EhInvalida() {
        var caso = Caso();

        await caso.ExecuteAsync(Cliente, Pergunta(new string('a', ResponderPerguntaRegras.TamanhoMaximoPergunta + 1), pagina: 1));

        Assert.False(caso.IsValid);
        Assert.Empty(_redator.Recebidos);
    }

    [Fact]
    public async Task HistoricoLongo_EhCortadoAntesDaChamada() {
        var pergunta = Pergunta("E se empatar?", pagina: 1);
        pergunta.Historico = [.. Enumerable.Range(0, 20).Select(i => new MensagemChatDTO {
            Papel = i % 2 == 0 ? PapelMensagemChat.Usuario : PapelMensagemChat.Assistente,
            Texto = $"mensagem {i}"
        })];

        await Caso().ExecuteAsync(Cliente, pergunta);

        Assert.Equal(ResponderPerguntaRegras.MaximoMensagensHistorico + 1, _redator.Recebidos.Single().Count);
        Assert.Equal(ChatRole.User, _redator.Recebidos.Single()[^1].Role);
    }

    [Fact]
    public void TextoParaBusca_LevaAPerguntaAnteriorDoUsuario() {
        List<MensagemChatDTO> historico = [
            new() { Papel = PapelMensagemChat.Usuario, Texto = "Como funciona o ladrão?" },
            new() { Papel = PapelMensagemChat.Assistente, Texto = "Quando sai 7..." },
        ];

        Assert.Equal("Como funciona o ladrão?\nE se eu tiver 8 cartas?",
                     ResponderPerguntaRegras.TextoParaBusca("E se eu tiver 8 cartas?", historico));
    }

    [Theory]
    [InlineData("[[OUTRO_JOGO:Catan]]", "Catan")]
    [InlineData("  [[ outro_jogo : Ticket to Ride ]] ", "Ticket to Ride")]
    [InlineData("[[OUTRO_JOGO:]]", null)]
    [InlineData("Resposta normal sobre o jogo.", null)]
    [InlineData(null, null)]
    public void ExtrairOutroJogo(string? resposta, string? esperado) {
        Assert.Equal(esperado, ResponderPerguntaRegras.ExtrairOutroJogo(resposta));
    }
}

public class ChatDTOJsonTests {

    // O front compara por nome ("ConfirmarJogo"); numero quebraria em silencio se a ordem mudar.
    [Fact]
    public void EnumsDoChat_ViajamComoTexto() {
        var json = System.Text.Json.JsonSerializer.Serialize(new RespostaChatDTO { Tipo = TipoRespostaChat.ConfirmarJogo });
        Assert.Contains("\"ConfirmarJogo\"", json);

        var pergunta = System.Text.Json.JsonSerializer.Deserialize<PerguntaChatDTO>(
            """{"Mensagem":"oi","Historico":[{"Papel":"Assistente","Texto":"ola"}]}""");
        Assert.Equal(PapelMensagemChat.Assistente, pergunta!.Historico[0].Papel);
    }
}
