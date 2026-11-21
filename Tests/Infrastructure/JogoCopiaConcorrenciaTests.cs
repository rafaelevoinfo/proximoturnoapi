using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class JogoCopiaConcorrenciaTests {
    // Sem o token, dois pedidos para a última cópia gravam os dois (UPDATE ... WHERE ID = @id).
    [Fact]
    public void StatusDaCopia_EhTokenDeConcorrencia() {
        var opcoes = new DbContextOptionsBuilder<DatabaseContext>()
            .UseMySQL("server=localhost;database=teste;user=teste;password=teste")
            .Options;
        using var db = new DatabaseContext(opcoes);

        var status = db.Model.FindEntityType(typeof(JogoCopia))!.FindProperty(nameof(JogoCopia.Status))!;

        Assert.True(status.IsConcurrencyToken);
    }
}
