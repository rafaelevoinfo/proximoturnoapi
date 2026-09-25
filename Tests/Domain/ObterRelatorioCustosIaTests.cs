using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ObterRelatorioCustosIaTests {

    private static GrupoUsoLlm Grupo(OperacaoLlm operacao, DesfechoLlm desfecho, string modelo, int requisicoes,
        decimal custo, int semCusto = 0, long entrada = 100, long saida = 10, long duracao = 1000) =>
        new(operacao, desfecho, modelo, requisicoes, semCusto, custo, entrada, saida, 1, 2, duracao);

    [Fact]
    public void SemGrupos_RelatorioZerado() {
        var relatorio = ObterRelatorioCustosIa.Montar([]);

        Assert.Equal(0m, relatorio.CustoTotalUsd);
        Assert.Equal(0, relatorio.TotalRequisicoes);
        Assert.Empty(relatorio.PorOperacao);
        Assert.Empty(relatorio.PorDesfecho);
        Assert.Empty(relatorio.PorModelo);
    }

    [Fact]
    public void Totais_SomamTodosOsGrupos() {
        var relatorio = ObterRelatorioCustosIa.Montar([
            Grupo(OperacaoLlm.Ocr, DesfechoLlm.Ok, "mistral/ocr", 2, 0.5m, entrada: 300, saida: 30, duracao: 2000),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.Truncado, "deepseek/deepseek-v4-flash", 3, 0.0000068600m, semCusto: 1)
        ]);

        Assert.Equal(0.5000068600m, relatorio.CustoTotalUsd);
        Assert.Equal(5, relatorio.TotalRequisicoes);
        Assert.Equal(1, relatorio.RequisicoesSemCusto);
        Assert.Equal(400, relatorio.TotalTokensEntrada);
        Assert.Equal(40, relatorio.TotalTokensSaida);
        Assert.Equal(2, relatorio.TotalTokensRaciocinio);
        Assert.Equal(4, relatorio.TotalTokensCache);
        Assert.Equal(3000, relatorio.DuracaoTotalMs);
    }

    [Fact]
    public void PorOperacao_JuntaModelosEDesfechosDaMesmaOperacao_OrdenadoPorCusto() {
        var relatorio = ObterRelatorioCustosIa.Montar([
            Grupo(OperacaoLlm.Embedding, DesfechoLlm.Ok, "openai/text-embedding-3-small", 5, 0.0001m),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.Ok, "deepseek/deepseek-v4-flash", 8, 0.2m),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.ErroHttp, "deepseek/deepseek-v4-flash", 1, 0m),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.Ok, "outro/modelo", 2, 0.1m)
        ]);

        Assert.Equal([OperacaoLlm.RevisaoMarkdown, OperacaoLlm.Embedding], relatorio.PorOperacao.Select(o => o.Operacao));
        var revisao = relatorio.PorOperacao[0];
        Assert.Equal("Revisão do markdown", revisao.Descricao);
        Assert.Equal(11, revisao.TotalRequisicoes);
        Assert.Equal(0.3m, revisao.CustoUsd);
        Assert.Equal(2, revisao.PorDesfecho.Count);
        Assert.Equal(10, revisao.PorDesfecho.Single(d => d.Desfecho == DesfechoLlm.Ok).TotalRequisicoes);
        Assert.Equal("Erro HTTP", revisao.PorDesfecho.Single(d => d.Desfecho == DesfechoLlm.ErroHttp).Descricao);
    }

    [Fact]
    public void PorDesfechoEPorModelo_AgregamEntreOperacoes() {
        var relatorio = ObterRelatorioCustosIa.Montar([
            Grupo(OperacaoLlm.Ocr, DesfechoLlm.Excecao, "mistral/ocr", 1, 0m),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.Excecao, "deepseek/deepseek-v4-flash", 2, 0m),
            Grupo(OperacaoLlm.RevisaoMarkdown, DesfechoLlm.Ok, "deepseek/deepseek-v4-flash", 4, 0.4m)
        ]);

        var excecao = relatorio.PorDesfecho.Single(d => d.Desfecho == DesfechoLlm.Excecao);
        Assert.Equal(3, excecao.TotalRequisicoes);
        Assert.Equal("Exceção", excecao.Descricao);

        Assert.Equal(["deepseek/deepseek-v4-flash", "mistral/ocr"], relatorio.PorModelo.Select(m => m.Modelo));
        Assert.Equal(6, relatorio.PorModelo[0].TotalRequisicoes);
        Assert.Equal(0.4m, relatorio.PorModelo[0].CustoUsd);
    }
}
