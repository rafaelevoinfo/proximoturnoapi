namespace ProximoTurnoApi.DTO;

public class RenovarPedidoRequest {
    public int PedidoId { get; set; }
    public List<JogoRenovacao>? Jogos { get; set; }
}

public class JogoRenovacao {
    public int JogoId { get; set; }
    public DateTime DataDevolucao { get; set; }
    public decimal Valor { get; set; }
}
