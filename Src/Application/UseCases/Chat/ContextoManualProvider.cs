using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>Um trecho que a busca devolveu e se ele passou do score mínimo e foi para o modelo.</summary>
public sealed record TrechoBuscado(TrechoManual Trecho, bool Usado);

/// <summary>
/// RAG do chat de regras como componente do agente (MAF): a cada execução, busca no manual do
/// jogo os trechos parecidos com a pergunta e os entrega como instruções da execução. Instruções
/// de <see cref="AIContext"/> são transitórias, então os trechos nunca entram na memória da
/// conversa — cada pergunta busca de novo.
/// <para>
/// Uma instância por execução: guarda o que buscou em <see cref="Buscados"/> para o caso de uso
/// devolver as fontes e gravar o turno.
/// </para>
/// </summary>
public sealed class ContextoManualProvider(JogoChat _jogo,
                                           InMemoryChatHistoryProvider _historico,
                                           IEmbeddingGenerator<string, Embedding<float>> _embedding,
                                           IManualVectorStore _vetores,
                                           float _scoreMinimo) : AIContextProvider {

    public const int QuantidadeTrechos = 6;

    public const string SemTrechos = "(nenhum trecho relacionado encontrado)";

    /// <summary>Todos os trechos da última busca, usados ou não. Vazio antes da execução.</summary>
    public IReadOnlyList<TrechoBuscado> Buscados { get; private set; } = [];

    /// <summary>Só os trechos que foram para o modelo.</summary>
    public IEnumerable<TrechoManual> Usados => Buscados.Where(b => b.Usado).Select(b => b.Trecho);

    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) {
        // O filtro padrao do provider entrega so as mensagens externas desta execucao: a
        // pergunta atual. A anterior vem da memoria da sessao.
        var pergunta = context.AIContext.Messages?.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var consulta = TextoParaBusca(pergunta, _historico.GetMessages(context.Session));

        var vetores = await _embedding.GenerateAsync([consulta], cancellationToken: cancellationToken);
        var trechos = await _vetores.BuscarAsync(_jogo.Id, vetores[0].Vector, QuantidadeTrechos, cancellationToken);

        Buscados = [.. trechos.Select(t => new TrechoBuscado(t, t.Score >= _scoreMinimo))];

        return new AIContext {
            Instructions = $"Trechos do manual de {_jogo.Nome}:\n{FormatarTrechos([.. Usados])}",
        };
    }

    /// <summary>
    /// Uma continuação curta ("e se empatar?") não acha nada sozinha no manual: a pergunta
    /// anterior do usuário, tirada da memória da conversa, vai junto para a busca.
    /// </summary>
    public static string TextoParaBusca(string mensagem, IEnumerable<ChatMessage> historico) {
        var anterior = historico.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        return string.IsNullOrWhiteSpace(anterior) ? mensagem : $"{anterior}\n{mensagem}";
    }

    public static string FormatarTrechos(IReadOnlyList<TrechoManual> trechos) {
        if (trechos.Count == 0) {
            return SemTrechos;
        }

        var texto = new StringBuilder();
        for (var i = 0; i < trechos.Count; i++) {
            texto.Append('[').Append(i + 1).Append("] ").AppendLine(trechos[i].Titulo);
            texto.AppendLine(trechos[i].Texto);
            texto.AppendLine();
        }

        return texto.ToString();
    }
}
