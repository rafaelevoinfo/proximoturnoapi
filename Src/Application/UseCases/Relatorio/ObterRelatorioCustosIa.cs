using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>Uma combinação operação/desfecho/modelo com as somas das suas chamadas.</summary>
public record GrupoUsoLlm(
    OperacaoLlm Operacao,
    DesfechoLlm Desfecho,
    string Modelo,
    int Requisicoes,
    int RequisicoesSemCusto,
    decimal CustoUsd,
    long TokensEntrada,
    long TokensSaida,
    long TokensRaciocinio,
    long TokensCache,
    long DuracaoMs);

public class ObterRelatorioCustosIa(DatabaseContext dbContext, ICotacaoDolar cotacaoDolar) : UseCaseBasico
{
    public async Task<RelatorioCustosIaDTO> ExecuteAsync(DateOnly? dataInicial, DateOnly? dataFinal, string? modelo)
    {
        var now = DateTime.Today;
        var start = dataInicial ?? DateOnly.FromDateTime(now.AddDays(-30));
        var end = dataFinal ?? DateOnly.FromDateTime(now);

        var startDateTime = start.ToDateTime(TimeOnly.MinValue);
        var endDateTimeNextDay = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var periodo = dbContext.UsosLlm
            .Where(u => u.Momento >= startDateTime && u.Momento < endDateTimeNextDay);

        var modelosDisponiveis = await periodo
            .Select(u => u.ModeloPedido)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        var query = string.IsNullOrWhiteSpace(modelo) ? periodo : periodo.Where(u => u.ModeloPedido == modelo);

        // O banco devolve no máximo operações x desfechos x modelos linhas; o resto é agregado em memória.
        var grupos = await query
            .GroupBy(u => new { u.Operacao, u.Desfecho, u.ModeloPedido })
            .Select(g => new GrupoUsoLlm(
                g.Key.Operacao,
                g.Key.Desfecho,
                g.Key.ModeloPedido,
                g.Count(),
                g.Count(u => u.CustoUsd == null),
                g.Sum(u => u.CustoUsd) ?? 0m,
                g.Sum(u => (long)u.TokensEntrada),
                g.Sum(u => (long)u.TokensSaida),
                g.Sum(u => (long)u.TokensRaciocinio),
                g.Sum(u => (long)u.TokensCache),
                g.Sum(u => (long)u.DuracaoMs)))
            .ToListAsync();

        var relatorio = Montar(grupos);
        relatorio.ModelosDisponiveis = modelosDisponiveis;
        relatorio.TopUsuariosChat = await TopUsuariosChatAsync(periodo);

        var cotacao = await cotacaoDolar.ObterAsync();
        relatorio.CotacaoUsdBrl = cotacao.Valor;
        relatorio.FonteCotacao = cotacao.Fonte;
        return relatorio;
    }

    private const int QuantidadeTopUsuarios = 10;

    private async Task<List<CustoIaUsuarioDTO>> TopUsuariosChatAsync(IQueryable<UsoLlm> periodo)
    {
        var top = await periodo
            .Where(u => u.IdUsuario != null)
            .GroupBy(u => u.IdUsuario!)
            .Select(g => new CustoIaUsuarioDTO
            {
                IdUsuario = g.Key,
                TotalRequisicoes = g.Count(),
                CustoUsd = g.Sum(u => u.CustoUsd) ?? 0m,
                CustoBrl = g.Sum(u => u.CustoBrl) ?? 0m
            })
            .OrderByDescending(u => u.CustoBrl)
            .Take(QuantidadeTopUsuarios)
            .ToListAsync();

        var ids = top.Select(u => u.IdUsuario).ToList();
        var emails = await dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email);

        foreach (var usuario in top)
        {
            usuario.Email = emails.GetValueOrDefault(usuario.IdUsuario);
        }

        return top;
    }

    public static RelatorioCustosIaDTO Montar(IReadOnlyCollection<GrupoUsoLlm> grupos)
    {
        return new RelatorioCustosIaDTO
        {
            CustoTotalUsd = grupos.Sum(g => g.CustoUsd),
            TotalRequisicoes = grupos.Sum(g => g.Requisicoes),
            RequisicoesSemCusto = grupos.Sum(g => g.RequisicoesSemCusto),
            TotalTokensEntrada = grupos.Sum(g => g.TokensEntrada),
            TotalTokensSaida = grupos.Sum(g => g.TokensSaida),
            TotalTokensRaciocinio = grupos.Sum(g => g.TokensRaciocinio),
            TotalTokensCache = grupos.Sum(g => g.TokensCache),
            DuracaoTotalMs = grupos.Sum(g => g.DuracaoMs),
            PorOperacao = grupos
                .GroupBy(g => g.Operacao)
                .Select(op => new CustoIaOperacaoDTO
                {
                    Operacao = op.Key,
                    Descricao = Descrever(op.Key),
                    CustoUsd = op.Sum(g => g.CustoUsd),
                    TotalRequisicoes = op.Sum(g => g.Requisicoes),
                    RequisicoesSemCusto = op.Sum(g => g.RequisicoesSemCusto),
                    TokensEntrada = op.Sum(g => g.TokensEntrada),
                    TokensSaida = op.Sum(g => g.TokensSaida),
                    TokensRaciocinio = op.Sum(g => g.TokensRaciocinio),
                    TokensCache = op.Sum(g => g.TokensCache),
                    DuracaoTotalMs = op.Sum(g => g.DuracaoMs),
                    PorDesfecho = PorDesfecho(op)
                })
                .OrderByDescending(o => o.CustoUsd)
                .ToList(),
            PorDesfecho = PorDesfecho(grupos),
            PorModelo = grupos
                .GroupBy(g => g.Modelo)
                .Select(m => new CustoIaModeloDTO
                {
                    Modelo = m.Key,
                    CustoUsd = m.Sum(g => g.CustoUsd),
                    TotalRequisicoes = m.Sum(g => g.Requisicoes)
                })
                .OrderByDescending(m => m.CustoUsd)
                .ToList()
        };
    }

    private static List<CustoIaDesfechoDTO> PorDesfecho(IEnumerable<GrupoUsoLlm> grupos) =>
        grupos
            .GroupBy(g => g.Desfecho)
            .OrderBy(d => d.Key)
            .Select(d => new CustoIaDesfechoDTO
            {
                Desfecho = d.Key,
                Descricao = Descrever(d.Key),
                TotalRequisicoes = d.Sum(g => g.Requisicoes),
                CustoUsd = d.Sum(g => g.CustoUsd)
            })
            .ToList();

    public static string Descrever(OperacaoLlm operacao) => operacao switch
    {
        OperacaoLlm.Ocr => "OCR",
        OperacaoLlm.RevisaoMarkdown => "Revisão do markdown",
        OperacaoLlm.Embedding => "Embedding",
        OperacaoLlm.ChatClassificacao => "Chat: classificação",
        OperacaoLlm.ChatEmbedding => "Chat: busca",
        OperacaoLlm.ChatResposta => "Chat: resposta",
        _ => operacao.ToString()
    };

    public static string Descrever(DesfechoLlm desfecho) => desfecho switch
    {
        DesfechoLlm.Ok => "Ok",
        DesfechoLlm.Truncado => "Truncado",
        DesfechoLlm.ErroDoModelo => "Erro do modelo",
        DesfechoLlm.ErroHttp => "Erro HTTP",
        DesfechoLlm.Excecao => "Exceção",
        _ => desfecho.ToString()
    };
}
