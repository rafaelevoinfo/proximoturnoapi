using System.Threading;
using System.Threading.Tasks;
using ProximoTurnoApi.Application.UseCases;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ContratoQueueTests
{
    [Fact]
    public async Task Enfileirar_DeveAdicionarItemNaFila_EDesenfileirarDeveRetornarNaOrdem()
    {
        // Arrange
        var queue = new ContratoQueue();

        // Act
        queue.Enfileirar(1);
        queue.Enfileirar(2);

        var job1 = await queue.DesenfileirarAsync(CancellationToken.None);
        var job2 = await queue.DesenfileirarAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, job1.IdPedido);
        Assert.Equal(0, job1.Tentativas);
        Assert.Equal(2, job2.IdPedido);
        Assert.Equal(0, job2.Tentativas);
    }

    [Fact]
    public async Task Enfileirar_ComFlagInativarExistente_DevePreservarFlagNoJob()
    {
        // Arrange
        var queue = new ContratoQueue();

        // Act
        queue.Enfileirar(42, 0, inativarExistente: true);
        var job = await queue.DesenfileirarAsync(CancellationToken.None);

        // Assert
        Assert.Equal(42, job.IdPedido);
        Assert.True(job.InativarExistente);
    }
}
