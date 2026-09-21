using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Application.Workers;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class IndexacaoManuaisWorkerTests {

    private static IndexacaoManuaisWorker Montar(FakeIndexacaoManualRepository repo, FakeManualVectorStore vetores, FakeManualQueue fila) {
        var servicos = new ServiceCollection();
        servicos.AddSingleton<IIndexacaoManualRepository>(repo);
        servicos.AddSingleton<IManualVectorStore>(vetores);
        var scopeFactory = servicos.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new IndexacaoManuaisWorker(NullLogger<IndexacaoManuaisWorker>.Instance, fila, scopeFactory);
    }

    /// <summary>
    /// A carga inicial e a reconciliacao rodam antes do loop principal, que so trava tentando
    /// desenfileirar (a FakeManualQueue nao suporta isso). Como os fakes sao sincronos, tudo
    /// roda ainda dentro do StartAsync; o delay so existe por seguranca.
    /// </summary>
    private static async Task RodarInicializacaoAsync(IndexacaoManuaisWorker worker) {
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CargaInicialEReconciliacao_EnfileiramSemDuplicarOJaCarregado() {
        var repo = new FakeIndexacaoManualRepository();
        repo.Elegiveis.Add(new ManualJob(1, 10)); // pendente do banco, vem na carga inicial
        repo.Adicionar(5, indexacao: new JogoLinkIndexacao { Url = "https://x/manual.pdf", Status = StatusIndexacao.Indexado }); // esperado, nao e orfao

        var vetores = new FakeManualVectorStore();
        vetores.IdsComVetores.AddRange([1, 2, 5]); // 1 ja veio na carga, 2 e orfao puro, 5 e esperado

        var fila = new FakeManualQueue();

        await RodarInicializacaoAsync(Montar(repo, vetores, fila));

        // (a) a carga inicial enfileira o que o repositorio devolveu
        Assert.Contains(fila.Enfileirados, j => j.IdJogoLink == 1 && j.IdJogo == 10);

        // (b) o orfao puro (2) e enfileirado com IdJogo = 0: a reconciliacao nao sabe o jogo
        Assert.Contains(fila.Enfileirados, j => j.IdJogoLink == 2 && j.IdJogo == 0);

        // (c) o id ja enfileirado na carga (1) e o esperado (5) nao entram de novo
        Assert.Equal(2, fila.Enfileirados.Count);
    }

    [Fact]
    public async Task FalhaNaCargaInicial_NaoImpedeAReconciliacao() {
        var repo = new FakeIndexacaoManualRepository { ErroAoListarElegiveis = new InvalidOperationException("banco fora do ar") };
        var vetores = new FakeManualVectorStore();
        vetores.IdsComVetores.Add(2); // orfao puro: nenhuma linha indexada no repositorio

        var fila = new FakeManualQueue();

        await RodarInicializacaoAsync(Montar(repo, vetores, fila));

        // A carga inicial falhou (fica sem nada dela), mas a reconciliacao roda do mesmo jeito.
        Assert.Contains(fila.Enfileirados, j => j.IdJogoLink == 2 && j.IdJogo == 0);
    }
}
