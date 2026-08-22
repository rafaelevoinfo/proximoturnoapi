using ProximoTurnoApi.Application.UseCases.RAG;

namespace ProximoTurnoApi.Tests.Fakes;

public class FakeManualQueue : IManualQueue {
    public List<ManualJob> Enfileirados { get; } = [];

    public void Enfileirar(ManualJob job) => Enfileirados.Add(job);

    public ValueTask<ManualJob> DesenfileirarAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
