using System.Threading;
using System.Threading.Tasks;

namespace ProximoTurnoApi.Application.UseCases;

public interface IContratoQueue
{
    void Enfileirar(int idPedido, int tentativas = 0, bool inativarExistente = false);
    ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken);
}
