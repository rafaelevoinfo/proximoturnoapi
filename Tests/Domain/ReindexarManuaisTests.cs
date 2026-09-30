using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ReindexarManuaisTests {

    private readonly FakeIndexacaoManualRepository _repo = new();
    private readonly FakeManualQueue _fila = new();

    private ReindexarManuais Montar() => new(NullLogger<ReindexarManuais>.Instance, _repo, _fila);

    [Fact]
    public async Task EnfileiraSoOsManuaisDeRegraDoJogo_Forcados() {
        _repo.Adicionar(1, idJogo: 7);
        _repo.Adicionar(2, idJogo: 7, tipo: TipoLink.Video);
        _repo.Adicionar(3, idJogo: 7, url: "");
        _repo.Adicionar(4, idJogo: 8);

        var quantidade = await Montar().ExecuteAsync(7);

        Assert.Equal(1, quantidade);
        Assert.Equal(new ManualJob(1, 7, Forcar: true), Assert.Single(_fila.Enfileirados));
    }

    [Fact]
    public async Task JogoSemManual_AvisaENaoEnfileira() {
        _repo.Adicionar(2, idJogo: 7, tipo: TipoLink.Video);
        var caso = Montar();

        var quantidade = await caso.ExecuteAsync(7);

        Assert.Equal(0, quantidade);
        Assert.False(caso.IsValid);
        Assert.Empty(_fila.Enfileirados);
    }
}
