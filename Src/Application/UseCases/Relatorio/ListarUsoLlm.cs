using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// As requisições de USO_LLM uma a uma, para o admin ver custo, tipo, tempo e desfecho de
/// cada chamada. O agregado fica em <see cref="ObterRelatorioCustosIa"/>.
/// </summary>
public class ListarUsoLlm(DatabaseContext dbContext) : UseCaseBasico {

    public const int TamanhoMaximo = 200;

    /// <summary>
    /// Uma linha de USO_LLM com o usuário que a gerou, quando houver. Inicializador e não
    /// construtor: o EF só traduz filtro e ordenação sobre membros de um MemberInit.
    /// </summary>
    public sealed class Linha {
        public required UsoLlm Uso { get; init; }
        public string? Email { get; init; }
        public string? Nome { get; init; }
    }

    public async Task<ConsultaUsoLlmDTO> ExecuteAsync(FiltroUsoLlm filtro, CancellationToken ct = default) {
        var filtradas = Consulta(dbContext, filtro);

        var total = await filtradas.CountAsync(ct);
        var resumo = new ResumoUsoLlmDTO {
            Requisicoes = total,
            RequisicoesComFalha = await filtradas.CountAsync(l => l.Uso.Desfecho != DesfechoLlm.Ok, ct),
            CustoUsd = await filtradas.SumAsync(l => l.Uso.CustoUsd, ct) ?? 0m,
            CustoBrl = await filtradas.SumAsync(l => l.Uso.CustoBrl, ct) ?? 0m,
            DuracaoTotalMs = await filtradas.SumAsync(l => (long)l.Uso.DuracaoMs, ct),
        };

        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);
        var pagina = Math.Max(filtro.Pagina, 1);
        var linhas = await Ordenar(filtradas, filtro.Ordem)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        return new ConsultaUsoLlmDTO {
            Pagina = new PaginaDTO<UsoLlmDTO> {
                Itens = linhas.Select(ParaDTO).ToList(),
                Total = total,
                Pagina = pagina,
                Tamanho = tamanho,
            },
            Resumo = resumo,
        };
    }

    /// <summary>
    /// Filtro com o usuário por LEFT JOIN: conta excluída continua aparecendo, só sem e-mail.
    /// Público para o teste compilar a consulta no provider MySQL.
    /// </summary>
    public static IQueryable<Linha> Consulta(DatabaseContext db, FiltroUsoLlm filtro) {
        var usos = db.UsosLlm.AsNoTracking();

        if (filtro.DataInicial is { } inicio) {
            var de = inicio.ToDateTime(TimeOnly.MinValue);
            usos = usos.Where(u => u.Momento >= de);
        }
        if (filtro.DataFinal is { } fim) {
            var ateExclusivo = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
            usos = usos.Where(u => u.Momento < ateExclusivo);
        }
        if (filtro.Operacao is { } operacao) usos = usos.Where(u => u.Operacao == operacao);
        if (filtro.Desfecho is { } desfecho) usos = usos.Where(u => u.Desfecho == desfecho);
        if (!string.IsNullOrWhiteSpace(filtro.Modelo)) {
            var modelo = filtro.Modelo.Trim();
            usos = usos.Where(u => u.ModeloPedido == modelo);
        }
        if (!string.IsNullOrWhiteSpace(filtro.TraceId)) {
            var trace = filtro.TraceId.Trim();
            usos = usos.Where(u => u.TraceId == trace);
        }

        var linhas = from u in usos
                     join usuario in db.Users on u.IdUsuario equals usuario.Id into encontrados
                     from usuario in encontrados.DefaultIfEmpty()
                     select new Linha {
                         Uso = u,
                         Email = usuario != null ? usuario.Email : null,
                         Nome = usuario != null ? usuario.Nome : null,
                     };

        if (!string.IsNullOrWhiteSpace(filtro.Busca)) {
            var busca = filtro.Busca.Trim();
            linhas = linhas.Where(l =>
                (l.Email != null && l.Email.Contains(busca)) ||
                (l.Nome != null && l.Nome.Contains(busca)) ||
                (l.Uso.Alvo != null && l.Uso.Alvo.Contains(busca)));
        }

        return linhas;
    }

    // Desempate pelo Id para a paginação não repetir nem pular linhas com o mesmo valor.
    public static IQueryable<Linha> Ordenar(IQueryable<Linha> linhas, OrdemUsoLlm ordem) => ordem switch {
        OrdemUsoLlm.MaiorCusto => linhas.OrderByDescending(l => l.Uso.CustoUsd).ThenByDescending(l => l.Uso.Id),
        OrdemUsoLlm.MaisLentas => linhas.OrderByDescending(l => l.Uso.DuracaoMs).ThenByDescending(l => l.Uso.Id),
        _ => linhas.OrderByDescending(l => l.Uso.Momento).ThenByDescending(l => l.Uso.Id),
    };

    private static UsoLlmDTO ParaDTO(Linha l) => new() {
        Id = l.Uso.Id,
        Momento = l.Uso.Momento,
        Operacao = l.Uso.Operacao,
        OperacaoDescricao = ObterRelatorioCustosIa.Descrever(l.Uso.Operacao),
        Desfecho = l.Uso.Desfecho,
        DesfechoDescricao = ObterRelatorioCustosIa.Descrever(l.Uso.Desfecho),
        ModeloPedido = l.Uso.ModeloPedido,
        ModeloRespondeu = l.Uso.ModeloRespondeu,
        Provider = l.Uso.Provider,
        Alvo = l.Uso.Alvo,
        IdJogo = l.Uso.IdJogo,
        IdUsuario = l.Uso.IdUsuario,
        UsuarioEmail = l.Email,
        UsuarioNome = l.Nome,
        TokensEntrada = l.Uso.TokensEntrada,
        TokensSaida = l.Uso.TokensSaida,
        TokensRaciocinio = l.Uso.TokensRaciocinio,
        TokensCache = l.Uso.TokensCache,
        CustoUsd = l.Uso.CustoUsd,
        CustoBrl = l.Uso.CustoBrl,
        CotacaoUsdBrl = l.Uso.CotacaoUsdBrl,
        DuracaoMs = l.Uso.DuracaoMs,
        Detalhe = l.Uso.Detalhe,
        TraceId = l.Uso.TraceId,
        IdGeracao = l.Uso.IdGeracao,
    };
}
