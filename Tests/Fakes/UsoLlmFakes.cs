using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Fakes;

public sealed class FakeUsoLlmRepository : IUsoLlmRepository {

    public List<UsoLlm> Gravados { get; } = [];
    public Exception? Erro { get; set; }

    public Task RegistrarAsync(UsoLlm uso) {
        Registrar(uso);
        return Task.CompletedTask;
    }

    public void Registrar(UsoLlm uso) {
        if (Erro is not null) {
            throw Erro;
        }

        Gravados.Add(uso);
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

/// <summary>Registrador de teste: guarda o que a policy entregou, sem banco.</summary>
public sealed class FakeRegistradorUsoLlm : IRegistradorUsoLlm {

    private readonly Lock _trava = new();

    public List<RegistroUsoLlm> Registros { get; } = [];
    public Exception? Erro { get; set; }

    public Task RegistrarAsync(RegistroUsoLlm registro) {
        Registrar(registro);
        return Task.CompletedTask;
    }

    public void Registrar(RegistroUsoLlm registro) {
        if (Erro is not null) {
            throw Erro;
        }

        // Duas chamadas simultaneas registram pela mesma instancia: a lista tem que aguentar.
        lock (_trava) {
            Registros.Add(registro);
        }
    }
}
