namespace ProximoTurnoApi.Application.UseCases;

public class ContratoJob
{
    public int IdPedido { get; }
    public int Tentativas { get; set; }
    public bool InativarExistente { get; }

    public ContratoJob(int idPedido, int tentativas = 0, bool inativarExistente = false)
    {
        IdPedido = idPedido;
        Tentativas = tentativas;
        InativarExistente = inativarExistente;
    }
}
