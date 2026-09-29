using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>Um trecho que a busca devolveu e se ele passou do score mínimo e foi para o modelo.</summary>
public sealed record TrechoBuscado(TrechoManual Trecho, bool Usado);

/// <summary>Uma chamada de ferramenta feita pelo modelo num turno, para o registro em CHAT_MENSAGEM.</summary>
public sealed record ChamadaFerramenta(string Ferramenta, int? IdJogo, string? Consulta, string? Erro, IReadOnlyList<TrechoBuscado> Trechos);

/// <summary>
/// Ferramentas do assistente de regras. O modelo decide quando chamar; o código garante o que
/// não pode depender dele: a busca é sempre de um jogo só, só de jogo com manual indexado, e
/// só entram trechos acima do score mínimo.
/// <para>
/// Uma instância por pergunta: guarda o catálogo lido e as chamadas feitas, para o caso de uso
/// gravar o turno.
/// </para>
/// </summary>
public sealed class FerramentasChat(IChatRegrasRepository _repositorio,
                                    IEmbeddingGenerator<string, Embedding<float>> _embedding,
                                    IManualVectorStore _vetores,
                                    float _scoreMinimo,
                                    string _idUsuario) {

    public const string NomeListarJogos = "listar_jogos";
    public const string NomeBuscarRegras = "buscar_regras";
    public const int QuantidadeTrechos = 6;
    public const string SemTrechos = "Nenhum trecho do manual tem relação com essa consulta.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<ChamadaFerramenta> _chamadas = [];
    private List<JogoChat>? _catalogo;

    public IReadOnlyList<ChamadaFerramenta> Chamadas => _chamadas;

    public IList<AITool> Todas() => [
        AIFunctionFactory.Create(ListarJogosAsync, NomeListarJogos,
            "Lista os jogos do catálogo da locadora, com id, nome e se o manual está disponível no assistente (temManual). " +
            "Use para descobrir de qual jogo o usuário está falando, inclusive por apelido, parte do nome ou nome com erro de digitação."),
        AIFunctionFactory.Create(BuscarRegrasAsync, NomeBuscarRegras,
            "Busca no manual de UM jogo os trechos mais relacionados à consulta. Use antes de responder qualquer dúvida de regra. " +
            "Só funciona para jogos com temManual = true."),
    ];

    public async Task<string> ListarJogosAsync(CancellationToken cancellationToken = default) {
        var catalogo = await CatalogoAsync();
        _chamadas.Add(new ChamadaFerramenta(NomeListarJogos, null, null, null, []));

        return JsonSerializer.Serialize(catalogo.Select(j => new { j.Id, j.Nome, j.TemManual }), Json);
    }

    public async Task<string> BuscarRegrasAsync(
        [Description("Id do jogo, como veio de listar_jogos.")] int idJogo,
        [Description("O que procurar no manual, em português, com os termos do próprio jogo (ex.: \"troca de recursos com o banco\").")] string consulta,
        CancellationToken cancellationToken = default) {

        var jogo = (await CatalogoAsync()).FirstOrDefault(j => j.Id == idJogo)
                   ?? await _repositorio.ObterJogoAsync(idJogo);

        string? erro = null;
        if (jogo is null) {
            erro = $"Não existe jogo com id {idJogo}. Use listar_jogos para achar o id certo.";
        } else if (!jogo.TemManual) {
            erro = $"O manual de {jogo.Nome} ainda não está disponível no assistente.";
        } else if (string.IsNullOrWhiteSpace(consulta)) {
            erro = "Informe o que procurar no manual.";
        }

        if (erro is not null) {
            _chamadas.Add(new ChamadaFerramenta(NomeBuscarRegras, idJogo, consulta, erro, []));
            return erro;
        }

        // O gasto do embedding vai para o jogo buscado, no credito de quem perguntou.
        IReadOnlyList<TrechoManual> trechos;
        using (EscopoUsoLlm.Abrir(jogo!.Id, null, $"Chat de regras / {jogo.Nome}", _idUsuario)) {
            var vetores = await _embedding.GenerateAsync([consulta], cancellationToken: cancellationToken);
            trechos = await _vetores.BuscarAsync(jogo.Id, vetores[0].Vector, QuantidadeTrechos, cancellationToken);
        }

        List<TrechoBuscado> buscados = [.. trechos.Select(t => new TrechoBuscado(t, t.Score >= _scoreMinimo))];
        _chamadas.Add(new ChamadaFerramenta(NomeBuscarRegras, jogo.Id, consulta, null, buscados));

        List<TrechoManual> usados = [.. buscados.Where(b => b.Usado).Select(b => b.Trecho)];
        return $"Trechos do manual de {jogo.Nome}:\n{FormatarTrechos(usados)}";
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

    // Lido uma vez por pergunta: o modelo pode listar e buscar varias vezes no mesmo turno.
    private async Task<List<JogoChat>> CatalogoAsync() => _catalogo ??= await _repositorio.ListarJogosAsync();
}
