namespace ProximoTurnoApi.Models;

public class AlertaAtrasoResponse
{
    public string? NomeCliente { get; set; }
    public string? TelefoneCliente { get; set; }
    public string? EmailCliente { get; set; }
    public DateTime DataPedido { get; set; }
    public string? NomeJogo { get; set; }
    public DateTime DataDevolucao { get; set; }
    public int DiasAtraso { get; set; }
}
