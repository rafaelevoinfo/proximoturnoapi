using System.Text.Json;
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

    private static readonly Chamar Listar = new(FerramentasChat.NomeListarJogos, new { });

    private static Chamar Buscar(int idJogo, string consulta) =>
        new(FerramentasChat.NomeBuscarRegras, new { idJogo, consulta });

    private readonly FakeChatRegrasRepository _repositorio = new();
    private readonly FakeChatConversaRepository _conversas = new();
    private readonly FakeManualVectorStore _vetores = new();
    private readonly FakeEmbeddingGenerator _embedding = new();
    private FakeChatClient _redator = new("Olá! Pode perguntar sobre as regras.");
    private IChatReducer? _redutor;

    public ResponderPerguntaRegrasTests() {
        _repositorio.Jogos.AddRange([
            new JogoChat(1, "Catan", true),
            new JogoChat(2, "Catan: Cidades e Cavaleiros", true),
            new JogoChat(10, "Reload", true),
            new JogoChat(11, "Azul", false),
        ]);
        _vetores.Trechos.AddRange([
            new TrechoManual(1, 10, "Catan > Comércio", "Troca 4:1 com o banco.", 0.9f),
            new TrechoManual(1, 11, "Catan > Ladrão", "Ao sair 7, mova o ladrão.", 0.12f),
            new TrechoManual(10, 30, "Reload > Objetivo", "Seja o último sobrevivente.", 0.8f),
        ]);
    }

    private ResponderPerguntaRegras Caso() =>
        new(NullLogger<ResponderPerguntaRegras>.Instance,
            new ObterSaldoChat(_repositorio, new FakeCotacaoDolar(5m), ConfiguracaoChat.Padrao),
            ConfiguracaoChat.Padrao,
            _repositorio, _conversas, _vetores, _redator, _embedding, _redutor);

    private static PerguntaChatDTO Pergunta(string mensagem, int? pagina = null, Guid? conversa = null) =>
        new() { Mensagem = mensagem, IdJogoPagina = pagina, IdConversa = conversa };

    /// <summary>O que a ferramenta devolveu ao modelo, lido da chamada seguinte a ele.</summary>
    private string ResultadoDaFerramenta(int chamadaDoModelo) =>
        string.Join("\n", _redator.Recebidos[chamadaDoModelo]
            .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
            .Select(r => r.Result?.ToString()));

    // ---- Fluxo com ferramentas ------------------------------------------------------------

    [Fact]
    public async Task PerguntaDeRegra_ModeloListaBuscaEResponde() {
        _redator = new FakeChatClient(Listar, Buscar(10, "objetivo do jogo"), "O objetivo é ser o último sobrevivente.");

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Qual o objetivo do Reload?"));

        Assert.Equal(TipoRespostaChat.Resposta, resposta!.Tipo);
        Assert.Equal("O objetivo é ser o último sobrevivente.", resposta.Texto);
        Assert.Contains("Reload", ResultadoDaFerramenta(1));
        Assert.Contains("Seja o último sobrevivente.", ResultadoDaFerramenta(2));
        Assert.Equal([10], _vetores.JogosBuscados);
        Assert.Equal(["objetivo do jogo"], _embedding.Textos);
    }

    [Fact]
    public async Task ModeloRecebeAsDuasFerramentasEAsInstrucoes() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Oi"));

        var opcoes = _redator.Opcoes.Single()!;
        Assert.Equal([FerramentasChat.NomeListarJogos, FerramentasChat.NomeBuscarRegras], opcoes.Tools!.Select(t => t.Name));
        Assert.Contains("Antes de responder qualquer dúvida de regra, chame buscar_regras", opcoes.Instructions);
        Assert.Contains("pergunte qual é antes de buscar", opcoes.Instructions);
    }

    // Cumprimento e assunto fora de regra nao buscam nada: nem embedding nem Qdrant.
    [Fact]
    public async Task Cumprimento_RespondeSemBuscar() {
        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Oi"));

        Assert.Equal("Olá! Pode perguntar sobre as regras.", resposta!.Texto);
        Assert.Single(_redator.Recebidos);
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_vetores.JogosBuscados);
    }

    [Fact]
    public async Task BuscaNuncaTrazTrechoDeOutroJogo() {
        _redator = new FakeChatClient(Buscar(1, "comércio"), "Troque 4:1.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Como troco com o banco?", pagina: 1));

        var resultado = ResultadoDaFerramenta(1);
        Assert.Contains("Troca 4:1 com o banco.", resultado);
        Assert.DoesNotContain("último sobrevivente", resultado);
    }

    [Fact]
    public async Task JogoSemManual_FerramentaAvisaSemBuscar() {
        _redator = new FakeChatClient(Buscar(11, "pontuação"), "O manual do Azul ainda não está disponível.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Como pontua no Azul?"));

        Assert.Contains("O manual de Azul ainda não está disponível no assistente.", ResultadoDaFerramenta(1));
        Assert.Empty(_embedding.Textos);
        Assert.Empty(_vetores.JogosBuscados);
    }

    [Fact]
    public async Task JogoInexistente_FerramentaMandaListar() {
        _redator = new FakeChatClient(Buscar(999, "regras"), "Não encontrei esse jogo.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Regras do jogo 999?"));

        Assert.Contains("Use listar_jogos", ResultadoDaFerramenta(1));
        Assert.Empty(_vetores.JogosBuscados);
    }

    [Fact]
    public async Task TrechoAbaixoDoScoreMinimo_NaoVaiParaOModeloMasFicaGravado() {
        _redator = new FakeChatClient(Buscar(1, "troca"), "Troque 4:1.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));

        var resultado = ResultadoDaFerramenta(1);
        Assert.Contains("Troca 4:1 com o banco.", resultado);
        Assert.DoesNotContain("mova o ladrão", resultado);

        using var registro = JsonDocument.Parse(_conversas.Mensagens.Single().Trechos!);
        var busca = Assert.Single(registro.RootElement.EnumerateArray());
        Assert.Equal(FerramentasChat.NomeBuscarRegras, busca.GetProperty("Ferramenta").GetString());
        Assert.Equal("troca", busca.GetProperty("Consulta").GetString());
        Assert.Equal([true, false], busca.GetProperty("Trechos").EnumerateArray().Select(t => t.GetProperty("Usado").GetBoolean()));
    }

    [Fact]
    public async Task NenhumTrechoRelacionado_FerramentaAvisa() {
        _vetores.Trechos.RemoveAll(t => t.IdJogo == 10);
        _redator = new FakeChatClient(Buscar(10, "receita de bolo"), "Não encontrei isso no manual.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Tem receita de bolo no Reload?"));

        Assert.Contains(FerramentasChat.SemTrechos, ResultadoDaFerramenta(1));
    }

    // Teto de idas e voltas: um modelo em loop nao pode gastar o credito do usuario.
    [Fact]
    public async Task ModeloEmLoop_ParaNoLimiteDeChamadas() {
        _redator = new FakeChatClient(Listar, Listar, Listar, Listar, Listar, Listar, Listar, Listar, "Desisti.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Qual jogo?"));

        Assert.True(_redator.Recebidos.Count <= ResponderPerguntaRegras.MaximoChamadasFerramenta + 1,
                    $"O modelo foi chamado {_redator.Recebidos.Count} vezes.");
    }

    // ---- Contexto da página --------------------------------------------------------------

    [Fact]
    public async Task JogoDaPagina_VaiComoContexto() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Oi", pagina: 1));

        Assert.Contains("página do jogo Catan (id 1)", _redator.Opcoes.Single()!.Instructions);
    }

    [Fact]
    public async Task SemPagina_ContextoDizQueNaoHaJogo() {
        await Caso().ExecuteAsync(Cliente, Pergunta("Oi"));

        Assert.Contains("não está na página de nenhum jogo", _redator.Opcoes.Single()!.Instructions);
    }

    [Fact]
    public void PaginaDeJogoSemManual_ContextoAvisa() {
        Assert.Contains("ainda não tem manual", ResponderPerguntaRegras.Contexto(new JogoChat(11, "Azul", false)));
    }

    // ---- Streaming -----------------------------------------------------------------------

    // So o texto do assistente vai para a tela: pedidos e resultados de ferramenta nao.
    [Fact]
    public async Task Streaming_SoOTextoFinalVaiParaATela() {
        _redator = new FakeChatClient(Listar, Buscar(10, "objetivo"), "O objetivo é ser o último sobrevivente.");
        var saida = new FakeSaidaChat();

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Qual o objetivo do Reload?"), saida);

        var cabecalho = Assert.Single(saida.Cabecalhos);
        Assert.Equal(resposta!.IdConversa, cabecalho.IdConversa);
        Assert.True(saida.Trechos.Count > 1);
        Assert.Equal("O objetivo é ser o último sobrevivente.", string.Concat(saida.Trechos));
    }

    [Fact]
    public async Task ModeloSemTexto_MandaMensagemPadrao() {
        _redator = new FakeChatClient("");
        var saida = new FakeSaidaChat();

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Oi"), saida);

        Assert.Equal(ResponderPerguntaRegras.MensagemSemResposta, resposta!.Texto);
        Assert.Equal([ResponderPerguntaRegras.MensagemSemResposta], saida.Trechos);
    }

    [Fact]
    public async Task CanceladoNoMeio_NaoGravaOTurno() {
        _redator = new FakeChatClient("Uma resposta comprida o bastante para vir em vários pedaços.");
        using var cancelamento = new CancellationTokenSource();
        var saida = new FakeSaidaChat { AoEscrever = cancelamento.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Caso().ExecuteAsync(Cliente, Pergunta("Oi"), saida, cancelamento.Token));

        Assert.Empty(_conversas.Mensagens);
    }

    // ---- Memória -------------------------------------------------------------------------

    [Fact]
    public async Task SegundaPergunta_ModeloRecebeAMemoriaSemAsFerramentas() {
        _redator = new FakeChatClient(Buscar(1, "comércio"), "Troque 4 iguais com o banco.", "Com porto é 3:1.");

        var primeira = await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1));
        var segunda = await Caso().ExecuteAsync(Cliente, Pergunta("E com porto?", pagina: 1, conversa: primeira!.IdConversa));

        Assert.Equal(primeira.IdConversa, segunda!.IdConversa);
        Assert.Equal([
            (ChatRole.User, "Posso trocar com o banco?"),
            (ChatRole.Assistant, "Troque 4 iguais com o banco."),
            (ChatRole.User, "E com porto?"),
        ], _redator.Recebidos[^1].Select(m => (m.Role, m.Text)));
        Assert.DoesNotContain("Troca 4:1 com o banco.", _conversas.Conversas.Single().Sessao);
    }

    // A conversa pode passar por varios jogos: cada busca e de um, a memoria e uma so.
    [Fact]
    public async Task ConversaPassaPorDoisJogos() {
        _redator = new FakeChatClient(Buscar(1, "comércio"), "Troque 4:1.", Buscar(10, "objetivo"), "Seja o último.");

        var primeira = await Caso().ExecuteAsync(Cliente, Pergunta("Como troco no Catan?"));
        await Caso().ExecuteAsync(Cliente, Pergunta("E qual o objetivo do Reload?", conversa: primeira!.IdConversa));

        Assert.Equal([1, 10], _vetores.JogosBuscados);
        Assert.Single(_conversas.Conversas);
    }

    [Fact]
    public async Task ConversaDeOutroUsuario_NaoEhLida() {
        var primeira = await Caso().ExecuteAsync(Cliente, Pergunta("Oi"));
        var outro = new UsuarioChat("usuario-2", "outro@teste.com", Admin: false);

        var resposta = await Caso().ExecuteAsync(outro, Pergunta("Oi de novo", conversa: primeira!.IdConversa));

        Assert.NotEqual(primeira.IdConversa, resposta!.IdConversa);
        Assert.Single(_redator.Recebidos[1]);
    }

    [Fact]
    public async Task Redutor_EncolheAMemoriaGravada() {
#pragma warning disable MEAI001 // redutor experimental do Microsoft.Extensions.AI, como no Program.cs
        _redutor = new MessageCountingChatReducer(2);
#pragma warning restore MEAI001
        _redator = new FakeChatClient("r1", "r2", "r3");

        var primeira = await Caso().ExecuteAsync(Cliente, Pergunta("p1"));
        await Caso().ExecuteAsync(Cliente, Pergunta("p2", conversa: primeira!.IdConversa));
        await Caso().ExecuteAsync(Cliente, Pergunta("p3", conversa: primeira.IdConversa));

        Assert.Equal(["p2", "r2", "p3"], _redator.Recebidos[2].Select(m => m.Text));
    }

    [Fact]
    public void SoConversa_TiraChamadasEResultadosDeFerramenta() {
        List<ChatMessage> resposta = [
            new(ChatRole.Assistant, [new FunctionCallContent("c1", FerramentasChat.NomeListarJogos)]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "[{\"id\":1}]")]),
            new(ChatRole.Assistant, "Resposta final."),
        ];

        var guardadas = ResponderPerguntaRegras.SoConversa(resposta).ToList();

        var unica = Assert.Single(guardadas);
        Assert.Equal(ChatRole.Assistant, unica.Role);
        Assert.Equal("Resposta final.", unica.Text);
    }

    // ---- Registro e crédito --------------------------------------------------------------

    [Fact]
    public async Task CadaTurno_GravaPerguntaRespostaEFerramentas() {
        _redator = new FakeChatClient(Listar, Buscar(10, "objetivo"), "Seja o último.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Objetivo do Reload?", pagina: 1));

        var conversa = Assert.Single(_conversas.Conversas);
        Assert.Equal("usuario-1", conversa.IdUsuario);
        Assert.Equal(1, conversa.IdJogo);

        var turno = Assert.Single(_conversas.Mensagens);
        Assert.Equal("Objetivo do Reload?", turno.Pergunta);
        Assert.Equal("Seja o último.", turno.Resposta);
        using var registro = JsonDocument.Parse(turno.Trechos!);
        Assert.Equal([FerramentasChat.NomeListarJogos, FerramentasChat.NomeBuscarRegras],
                     registro.RootElement.EnumerateArray().Select(c => c.GetProperty("Ferramenta").GetString()));
    }

    // O gasto de cada chamada vai para o credito de quem perguntou; o embedding, no jogo buscado.
    [Fact]
    public async Task ChamadasPagasCarregamOUsuario() {
        _redator = new FakeChatClient(Buscar(10, "objetivo"), "Seja o último.");

        await Caso().ExecuteAsync(Cliente, Pergunta("Objetivo do Reload?", pagina: 1));

        Assert.All(_redator.Alvos, alvo => Assert.Equal("usuario-1", alvo?.IdUsuario));
        var alvoEmbedding = Assert.Single(_embedding.Alvos);
        Assert.Equal("usuario-1", alvoEmbedding?.IdUsuario);
        Assert.Equal(10, alvoEmbedding?.IdJogo);
    }

    [Fact]
    public async Task SaldoEsgotado_MensagemAmigavelSemChamarLlmNenhum() {
        _repositorio.Gasto = 100m;
        var saida = new FakeSaidaChat();

        var resposta = await Caso().ExecuteAsync(Cliente, Pergunta("Posso trocar com o banco?", pagina: 1), saida);

        Assert.Equal(TipoRespostaChat.SaldoEsgotado, resposta!.Tipo);
        Assert.Equal(ResponderPerguntaRegras.MensagemSaldoEsgotado, resposta.Texto);
        Assert.Empty(_redator.Recebidos);
        Assert.Empty(saida.Cabecalhos);
    }

    [Fact]
    public async Task Admin_SemLimiteDeSaldo() {
        _repositorio.Gasto = 100m;

        var resposta = await Caso().ExecuteAsync(Admin, Pergunta("Oi"));

        Assert.Equal(TipoRespostaChat.Resposta, resposta!.Tipo);
    }

    [Fact]
    public async Task PerguntaVazia_EhInvalida() {
        var caso = Caso();

        var resposta = await caso.ExecuteAsync(Cliente, Pergunta("   "));

        Assert.Null(resposta);
        Assert.False(caso.IsValid);
    }

    [Fact]
    public async Task PerguntaLongaDemais_EhInvalida() {
        var caso = Caso();

        await caso.ExecuteAsync(Cliente, Pergunta(new string('a', ResponderPerguntaRegras.TamanhoMaximoPergunta + 1)));

        Assert.False(caso.IsValid);
        Assert.Empty(_redator.Recebidos);
    }
}

public class ChatDTOJsonTests {

    // O front compara por nome ("Resposta", "SaldoEsgotado"); numero quebraria em silencio.
    [Fact]
    public void EnumsDoChat_ViajamComoTexto() {
        var json = JsonSerializer.Serialize(new RespostaChatDTO { Tipo = TipoRespostaChat.SaldoEsgotado });
        Assert.Contains("\"SaldoEsgotado\"", json);

        Assert.Equal(TipoRespostaChat.Resposta, JsonSerializer.Deserialize<TipoRespostaChat>("\"Resposta\""));
    }
}
