using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

/// <summary>Gastos com LLM no período, agregados a partir da USO_LLM.</summary>
public class RelatorioCustosIaDTO
{
    public decimal CustoTotalUsd { get; set; }
    public int TotalRequisicoes { get; set; }
    /// <summary>Chamadas com CUSTO_USD nulo: o custo não entra na soma porque não se sabe quanto foi.</summary>
    public int RequisicoesSemCusto { get; set; }
    public long TotalTokensEntrada { get; set; }
    public long TotalTokensSaida { get; set; }
    public long TotalTokensRaciocinio { get; set; }
    public long TotalTokensCache { get; set; }
    public long DuracaoTotalMs { get; set; }
    /// <summary>Modelos pedidos no período, sem considerar o filtro de modelo, para alimentar o filtro.</summary>
    public List<string> ModelosDisponiveis { get; set; } = [];
    public List<CustoIaOperacaoDTO> PorOperacao { get; set; } = [];
    public List<CustoIaDesfechoDTO> PorDesfecho { get; set; } = [];
    public List<CustoIaModeloDTO> PorModelo { get; set; } = [];
    /// <summary>Quem mais gastou no chat de regras no período, sem considerar o filtro de modelo.</summary>
    public List<CustoIaUsuarioDTO> TopUsuariosChat { get; set; } = [];
    /// <summary>Cotação que o chat está usando agora para debitar o crédito.</summary>
    public decimal CotacaoUsdBrl { get; set; }
    public FonteCotacao FonteCotacao { get; set; }
}

public class CustoIaUsuarioDTO
{
    public string IdUsuario { get; set; } = string.Empty;
    /// <summary>Nulo quando a conta já foi excluída: o gasto fica, o dono não.</summary>
    public string? Email { get; set; }
    public int TotalRequisicoes { get; set; }
    public decimal CustoUsd { get; set; }
    public decimal CustoBrl { get; set; }
}

public class CustoIaOperacaoDTO
{
    public OperacaoLlm Operacao { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal CustoUsd { get; set; }
    public int TotalRequisicoes { get; set; }
    public int RequisicoesSemCusto { get; set; }
    public long TokensEntrada { get; set; }
    public long TokensSaida { get; set; }
    public long TokensRaciocinio { get; set; }
    public long TokensCache { get; set; }
    public long DuracaoTotalMs { get; set; }
    public List<CustoIaDesfechoDTO> PorDesfecho { get; set; } = [];
}

public class CustoIaDesfechoDTO
{
    public DesfechoLlm Desfecho { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public int TotalRequisicoes { get; set; }
    public decimal CustoUsd { get; set; }
}

public class CustoIaModeloDTO
{
    public string Modelo { get; set; } = string.Empty;
    public decimal CustoUsd { get; set; }
    public int TotalRequisicoes { get; set; }
}
