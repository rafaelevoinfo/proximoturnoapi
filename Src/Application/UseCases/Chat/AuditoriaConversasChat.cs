using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Auditoria do chat de regras pelo admin: o que cada usuário perguntou, o que o modelo
/// respondeu e quais trechos do manual a busca trouxe para isso.
/// </summary>
public class AuditoriaConversasChat(DatabaseContext dbContext) : UseCaseBasico {

    public const int TamanhoMaximo = 100;

    public async Task<PaginaDTO<ConversaChatResumoDTO>> ListarAsync(FiltroConversasChat filtro, CancellationToken ct = default) {
        var consulta = ConsultaConversas(dbContext, filtro);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);
        var pagina = Math.Max(filtro.Pagina, 1);

        return new PaginaDTO<ConversaChatResumoDTO> {
            Total = await consulta.CountAsync(ct),
            Itens = await consulta
                .OrderByDescending(c => c.DataAtualizacao)
                .Skip((pagina - 1) * tamanho)
                .Take(tamanho)
                .ToListAsync(ct),
            Pagina = pagina,
            Tamanho = tamanho,
        };
    }

    public async Task<ConversaChatDTO?> ObterAsync(Guid chave, CancellationToken ct = default) {
        var conversa = await ConsultaConversas(dbContext, new FiltroConversasChat())
            .FirstOrDefaultAsync(c => c.Chave == chave, ct);
        if (conversa is null) return null;

        var mensagens = await (
            from c in dbContext.ChatConversas
            join m in dbContext.ChatMensagens on c.Id equals m.IdConversa
            where c.Chave == chave
            orderby m.Momento, m.Id
            select m
        ).AsNoTracking().ToListAsync(ct);

        var lidas = mensagens.Select(m => (Mensagem: m, Chamadas: LerChamadas(m.Trechos))).ToList();

        // Nomes de jogos e manuais citados nas buscas, numa consulta para cada.
        var idsJogo = lidas.SelectMany(l => l.Chamadas ?? []).Select(c => c.IdJogo).OfType<int>().Distinct().ToList();
        var idsLink = lidas.SelectMany(l => l.Chamadas ?? []).SelectMany(c => c.Trechos).Select(t => t.IdJogoLink).Distinct().ToList();
        var jogos = idsJogo.Count == 0 ? new Dictionary<int, string>() : await dbContext.Jogos.AsNoTracking()
            .Where(j => idsJogo.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.Nome, ct);
        var manuais = idsLink.Count == 0 ? new Dictionary<int, string>() : await dbContext.JogoLinks.AsNoTracking()
            .Where(l => idsLink.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Titulo, ct);

        return new ConversaChatDTO {
            Conversa = conversa,
            Mensagens = lidas.Select(l => new MensagemChatAuditoriaDTO {
                Momento = l.Mensagem.Momento,
                Pergunta = l.Mensagem.Pergunta,
                Resposta = l.Mensagem.Resposta,
                Tipo = l.Mensagem.Tipo,
                Chamadas = (l.Chamadas ?? []).Select(c => c with {
                    NomeJogo = c.IdJogo is { } id ? jogos.GetValueOrDefault(id) : null,
                    Trechos = c.Trechos.Select(t => t with { Manual = manuais.GetValueOrDefault(t.IdJogoLink) }).ToList(),
                }).ToList(),
                TrechosBrutos = l.Chamadas is null ? l.Mensagem.Trechos : null,
            }).ToList(),
        };
    }

    /// <summary>Conversas com usuário e jogo por LEFT JOIN: conta ou jogo excluído não some da lista.</summary>
    public static IQueryable<ConversaChatResumoDTO> ConsultaConversas(DatabaseContext db, FiltroConversasChat filtro) {
        var conversas = db.ChatConversas.AsNoTracking();

        // Conversas com atividade no período: começaram antes do fim e foram usadas depois do início.
        if (filtro.DataInicial is { } inicio) {
            var de = inicio.ToDateTime(TimeOnly.MinValue);
            conversas = conversas.Where(c => c.DataAtualizacao >= de);
        }
        if (filtro.DataFinal is { } fim) {
            var ateExclusivo = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
            conversas = conversas.Where(c => c.DataCriacao < ateExclusivo);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Busca)) {
            var busca = filtro.Busca.Trim();
            conversas = conversas.Where(c => db.ChatMensagens.Any(m =>
                m.IdConversa == c.Id && (m.Pergunta.Contains(busca) || (m.Resposta != null && m.Resposta.Contains(busca)))));
        }

        var resumos = from c in conversas
                      join usuario in db.Users on c.IdUsuario equals usuario.Id into usuarios
                      from usuario in usuarios.DefaultIfEmpty()
                      join jogo in db.Jogos on c.IdJogo equals jogo.Id into jogos
                      from jogo in jogos.DefaultIfEmpty()
                      select new ConversaChatResumoDTO {
                          Chave = c.Chave,
                          IdUsuario = c.IdUsuario,
                          UsuarioEmail = usuario != null ? usuario.Email : null,
                          UsuarioNome = usuario != null ? usuario.Nome : null,
                          IdJogo = c.IdJogo,
                          NomeJogo = jogo != null ? jogo.Nome : null,
                          DataCriacao = c.DataCriacao,
                          DataAtualizacao = c.DataAtualizacao,
                          QuantidadeMensagens = db.ChatMensagens.Count(m => m.IdConversa == c.Id),
                          PrimeiraPergunta = db.ChatMensagens
                              .Where(m => m.IdConversa == c.Id)
                              .OrderBy(m => m.Momento)
                              .Select(m => m.Pergunta)
                              .FirstOrDefault(),
                      };

        if (!string.IsNullOrWhiteSpace(filtro.Usuario)) {
            var usuarioBusca = filtro.Usuario.Trim();
            resumos = resumos.Where(r =>
                (r.UsuarioEmail != null && r.UsuarioEmail.Contains(usuarioBusca)) ||
                (r.UsuarioNome != null && r.UsuarioNome.Contains(usuarioBusca)) ||
                r.IdUsuario == usuarioBusca);
        }

        return resumos;
    }

    /// <summary>
    /// Lê CHAT_MENSAGEM.TRECHOS nos três formatos que já foram gravados:
    /// <list type="bullet">
    /// <item>atual: lista de chamadas de ferramenta {Ferramenta, IdJogo, Consulta, Erro, Trechos[]};</item>
    /// <item>anterior: lista de trechos {IdJogoLink, Titulo, Texto, Score, Usado}, da busca automática;</item>
    /// <item>primeiro: o mesmo, sem Usado.</item>
    /// </list>
    /// Os dois antigos viram uma chamada sem ferramenta. Null quando o JSON não é reconhecido.
    /// </summary>
    public static List<ChamadaFerramentaAuditoriaDTO>? LerChamadas(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var itens = doc.RootElement.EnumerateArray().ToList();
            if (itens.Count == 0) return [];

            // Formato antigo: os próprios itens já são trechos.
            if (itens.All(i => i.ValueKind == JsonValueKind.Object && i.TryGetProperty("Texto", out _))) {
                return [new ChamadaFerramentaAuditoriaDTO { Trechos = itens.Select(LerTrecho).ToList() }];
            }

            return itens.Where(i => i.ValueKind == JsonValueKind.Object).Select(i => new ChamadaFerramentaAuditoriaDTO {
                Ferramenta = Texto(i, "Ferramenta"),
                IdJogo = i.TryGetProperty("IdJogo", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : null,
                Consulta = Texto(i, "Consulta"),
                Erro = Texto(i, "Erro"),
                Trechos = i.TryGetProperty("Trechos", out var t) && t.ValueKind == JsonValueKind.Array
                    ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(LerTrecho).ToList()
                    : [],
            }).ToList();
        } catch (JsonException) {
            return null;
        }
    }

    private static TrechoAuditoriaDTO LerTrecho(JsonElement e) => new() {
        IdJogoLink = e.TryGetProperty("IdJogoLink", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0,
        Titulo = Texto(e, "Titulo"),
        Texto = Texto(e, "Texto") ?? "",
        Score = e.TryGetProperty("Score", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetSingle() : 0,
        Usado = e.TryGetProperty("Usado", out var u) && u.ValueKind is JsonValueKind.True or JsonValueKind.False ? u.GetBoolean() : null,
    };

    private static string? Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
