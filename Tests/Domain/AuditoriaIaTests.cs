using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class AuditoriaIaTests {

    // O provider MySQL da Oracle recusa algumas consultas só ao traduzir; ToQueryString
    // traduz sem precisar de banco (ver ChatRegrasRepositorySqlTests).
    private static DatabaseContext ContextoMySql() => new(new DbContextOptionsBuilder<DatabaseContext>()
        .UseMySQL("server=localhost;database=teste;user=teste;password=teste")
        .Options);

    [Theory]
    [InlineData(OrdemUsoLlm.Recentes, "`MOMENTO` DESC")]
    [InlineData(OrdemUsoLlm.MaiorCusto, "`CUSTO_USD` DESC")]
    [InlineData(OrdemUsoLlm.MaisLentas, "`DURACAO_MS` DESC")]
    public void Requisicoes_ComTodosOsFiltros_CompilamNoMySql(OrdemUsoLlm ordem, string ordenacao) {
        using var db = ContextoMySql();
        var filtro = new FiltroUsoLlm {
            DataInicial = new DateOnly(2026, 10, 1),
            DataFinal = new DateOnly(2026, 10, 3),
            Operacao = OperacaoLlm.ChatResposta,
            Desfecho = DesfechoLlm.ErroHttp,
            Modelo = "deepseek/deepseek-v4-flash",
            Busca = "maria",
            TraceId = "abc",
        };

        var sql = ListarUsoLlm.Ordenar(ListarUsoLlm.Consulta(db, filtro), ordem).Skip(50).Take(50).ToQueryString();

        Assert.Contains("USO_LLM", sql);
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains(ordenacao, sql);
    }

    [Fact]
    public void Conversas_ComTodosOsFiltros_CompilamNoMySql() {
        using var db = ContextoMySql();
        var filtro = new FiltroConversasChat {
            DataInicial = new DateOnly(2026, 10, 1),
            DataFinal = new DateOnly(2026, 10, 3),
            Usuario = "maria@",
            Busca = "pontuação",
        };

        var sql = AuditoriaConversasChat.ConsultaConversas(db, filtro)
            .OrderByDescending(c => c.DataAtualizacao).Skip(30).Take(30).ToQueryString();

        Assert.Contains("CHAT_CONVERSA", sql);
        Assert.Contains("CHAT_MENSAGEM", sql);
        Assert.Contains("JOGO", sql);
    }

    [Fact]
    public void LerChamadas_FormatoAtual_ComFerramentas() {
        const string json = """
            [{"Ferramenta":"listar_jogos","IdJogo":null,"Consulta":null,"Erro":null,"Trechos":[]},
             {"Ferramenta":"buscar_regras","IdJogo":7,"Consulta":"pontuação final","Erro":null,
              "Trechos":[{"IdJogoLink":3,"Titulo":"Catan > Fim de jogo","Texto":"Vence quem...","Score":0.81,"Usado":true},
                         {"IdJogoLink":3,"Titulo":"Catan > Preparação","Texto":"Monte o tabuleiro","Score":0.32,"Usado":false}]}]
            """;

        var chamadas = AuditoriaConversasChat.LerChamadas(json)!;

        Assert.Equal(["listar_jogos", "buscar_regras"], chamadas.Select(c => c.Ferramenta));
        var busca = chamadas[1];
        Assert.Equal(7, busca.IdJogo);
        Assert.Equal("pontuação final", busca.Consulta);
        Assert.Equal([true, false], busca.Trechos.Select(t => t.Usado));
        Assert.Equal(0.81f, busca.Trechos[0].Score);
        Assert.Equal("Catan > Fim de jogo", busca.Trechos[0].Titulo);
    }

    [Fact]
    public void LerChamadas_FormatoAntigo_ViraUmaBuscaSemFerramenta() {
        const string comUsado = """[{"IdJogoLink":3,"Titulo":"T","Texto":"a","Score":0.5,"Usado":true}]""";
        const string semUsado = """[{"IdJogo":7,"IdJogoLink":3,"Titulo":"T","Texto":"a","Score":0.5}]""";

        var a = Assert.Single(AuditoriaConversasChat.LerChamadas(comUsado)!);
        Assert.Null(a.Ferramenta);
        Assert.True(Assert.Single(a.Trechos).Usado);

        var b = Assert.Single(AuditoriaConversasChat.LerChamadas(semUsado)!);
        Assert.Null(Assert.Single(b.Trechos).Usado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    public void LerChamadas_Vazio(string? json) =>
        Assert.Empty(AuditoriaConversasChat.LerChamadas(json)!);

    [Theory]
    [InlineData("não é json")]
    [InlineData("{\"x\":1}")]
    public void LerChamadas_Irreconhecivel_DevolveNull(string json) =>
        Assert.Null(AuditoriaConversasChat.LerChamadas(json));
}
