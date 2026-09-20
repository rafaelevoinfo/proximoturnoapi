using System.Threading.Channels;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.RAG;

public interface IManualQueue {
    void Enfileirar(ManualJob job);
    ValueTask<ManualJob> DesenfileirarAsync(CancellationToken cancellationToken);
}

public static class ManualQueueExtensions {

    /// <summary>
    /// Pede a sincronização de todos os links do jogo e dos que acabaram de ser removidos.
    /// Não filtra por tipo nem por estado de propósito: quem decide é o SincronizarManual,
    /// com o banco na mão. Chamar depois do SaveAsync, quando os links novos já têm Id.
    /// </summary>
    public static void EnfileirarSincronizacao(this IManualQueue queue, Jogo jogo, IEnumerable<int>? idsRemovidos = null) {
        foreach (var link in jogo.Links ?? []) {
            queue.Enfileirar(new ManualJob(link.Id, jogo.Id));
        }

        foreach (var id in idsRemovidos ?? []) {
            queue.Enfileirar(new ManualJob(id, jogo.Id));
        }
    }
}

public class ManualQueue : IManualQueue {
    private readonly Channel<ManualJob> _channel;

    public ManualQueue() {
        // Um único consumidor (o worker) e vários produtores (os use cases de jogo).
        _channel = Channel.CreateUnbounded<ManualJob>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enfileirar(ManualJob job) {
        _channel.Writer.TryWrite(job);
    }

    public ValueTask<ManualJob> DesenfileirarAsync(CancellationToken cancellationToken) {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
