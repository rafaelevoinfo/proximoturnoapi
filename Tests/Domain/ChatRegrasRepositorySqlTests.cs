using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace ProximoTurnoApi.Tests.Domain;

// O provider MySQL da Oracle so recusa consulta ao compilar, e compilar nao precisa de banco:
// ToQueryString pega aqui o que em producao so apareceria como erro na tela.
public class ChatRegrasRepositorySqlTests(ITestOutputHelper saida) {

    [Fact]
    public void ListarJogos_CompilaNoProviderMySql() {
        var opcoes = new DbContextOptionsBuilder<DatabaseContext>()
            .UseMySQL("server=localhost;database=teste;user=teste;password=teste")
            .Options;
        using var db = new DatabaseContext(opcoes);

        var sql = ChatRegrasRepository.ConsultaJogos(db).ToQueryString();
        saida.WriteLine(sql);

        Assert.Contains("JOGO_COPIA", sql);
        Assert.Contains("JOGO_LINK_INDEXACAO", sql);
    }
}
