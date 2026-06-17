using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProximoTurnoApi.Application.UseCases;

public class ContratoQueue : IContratoQueue
{
    private readonly Channel<ContratoJob> _channel;

    public ContratoQueue()
    {
        _channel = Channel.CreateUnbounded<ContratoJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enfileirar(int idPedido, int tentativas = 0, bool inativarExistente = false)
    {
        _channel.Writer.TryWrite(new ContratoJob(idPedido, tentativas, inativarExistente));
    }

    public ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
