namespace ProximoTurnoApi.Application.DTOs;

public class DashboardReportDTO
{
    public decimal TotalRecebido { get; set; }
    public List<FaturamentoMensalDTO> FaturamentoPorMes { get; set; } = [];
    public List<FaturamentoJogoDTO> FaturamentoPorJogo { get; set; } = [];
    public List<TopClienteDTO> TopClientes { get; set; } = [];
}

public class FaturamentoMensalDTO
{
    public string MesAno { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

public class FaturamentoJogoDTO
{
    public int JogoId { get; set; }
    public string NomeJogo { get; set; } = string.Empty;
    public decimal ValorGerado { get; set; }
}

public class TopClienteDTO
{
    public int ClienteId { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public decimal ValorGasto { get; set; }
    public int QuantidadeJogosAlugados { get; set; }
}
