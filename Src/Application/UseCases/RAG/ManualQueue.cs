using System.Threading.Channels;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.RAG;

public interface IManualQueue {
    void Enfileirar(ManualJob job);
    ValueTask<ManualJob> DesenfileirarAsync(CancellationToken cancellationToken);
}

public static class ManualQueueExtensions {

    /// <summary>
    /// Enfileira os manuais (links do tipo Regra) do jogo que ainda não foram indexados.
    /// Deve ser chamado depois do SaveAsync, quando o EF já atribuiu os Ids dos links novos.
    /// </summary>
    public static void EnfileirarManuaisPendentes(this IManualQueue queue, Jogo jogo) {
        if (jogo.Links is null) {
            return;
        }

        foreach (var link in jogo.Links) {
            if (link.Tipo == TipoLink.Regra && !link.Indexado && !string.IsNullOrWhiteSpace(link.Url)) {
                queue.Enfileirar(new ManualJob(link.Id, jogo.Id, link.Url));
            }
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
