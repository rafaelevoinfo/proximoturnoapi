using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

/// <summary>Qual etapa paga gerou a chamada.</summary>
public enum OperacaoLlm : short {
    Ocr = 0,
    RevisaoMarkdown = 1,
    Embedding = 2
}

/// <summary>Como a chamada terminou. Só <see cref="Ok"/> significa que o gasto rendeu algo.</summary>
public enum DesfechoLlm : short {
    /// <summary>Resposta completa: finish_reason "stop", ou ausente no caso do embedding.</summary>
    Ok = 0,
    /// <summary>finish_reason "length": foi cobrada e o chamador descartou.</summary>
    Truncado = 1,
    /// <summary>Qualquer outro finish_reason, incluindo o "error" da OpenRouter.</summary>
    ErroDoModelo = 2,
    /// <summary>Resposta com status fora de 2xx.</summary>
    ErroHttp = 3,
    /// <summary>Não houve resposta: timeout, DNS, socket. Sem tokens e sem custo.</summary>
    Excecao = 4
}

/// <summary>
/// Uma linha por tentativa de chamada a LLM: o que estava sendo feito, quanto gastou e como
/// terminou. Sem foreign key para o link de propósito — apagar um manual não pode apagar o
/// registro do que ele custou.
/// </summary>
[Table("USO_LLM")]
public class UsoLlm : BaseModel {

    [Column("MOMENTO")]
    public DateTime Momento { get; set; }

    /// <summary>Trace id da Activity, o mesmo que o Serilog imprime em cada linha de log.</summary>
    [Column("TRACE_ID"), MaxLength(32)]
    public string? TraceId { get; set; }

    [Column("OPERACAO")]
    public OperacaoLlm Operacao { get; set; }

    [Column("ID_JOGO")]
    public int? IdJogo { get; set; }

    [Column("ID_JOGO_LINK")]
    public int? IdJogoLink { get; set; }

    /// <summary>"Nome do jogo / título do manual", para ler o ledger sem precisar de join.</summary>
    [Column("ALVO"), MaxLength(200)]
    public string? Alvo { get; set; }

    [Column("MODELO_PEDIDO"), MaxLength(100)]
    public required string ModeloPedido { get; set; }

    [Column("MODELO_RESPONDEU"), MaxLength(100)]
    public string? ModeloRespondeu { get; set; }

    /// <summary>Quem atendeu. O mesmo modelo muda de provider entre chamadas, e o preço com ele.</summary>
    [Column("PROVIDER"), MaxLength(60)]
    public string? Provider { get; set; }

    [Column("TOKENS_ENTRADA")]
    public int TokensEntrada { get; set; }

    [Column("TOKENS_SAIDA")]
    public int TokensSaida { get; set; }

    [Column("TOKENS_RACIOCINIO")]
    public int TokensRaciocinio { get; set; }

    [Column("TOKENS_CACHE")]
    public int TokensCache { get; set; }

    /// <summary>Nulo é "não sei quanto custou"; zero é "não custou".</summary>
    [Column("CUSTO_USD")]
    public decimal? CustoUsd { get; set; }

    [Column("DURACAO_MS")]
    public int DuracaoMs { get; set; }

    [Column("DESFECHO")]
    public DesfechoLlm Desfecho { get; set; }

    /// <summary>finish_reason cru, trecho do corpo do erro HTTP, ou tipo e mensagem da exceção.</summary>
    [Column("DETALHE"), MaxLength(300)]
    public string? Detalhe { get; set; }

    /// <summary>O `gen-...` da OpenRouter, para auditar a chamada no painel deles.</summary>
    [Column("ID_GERACAO"), MaxLength(80)]
    public string? IdGeracao { get; set; }
}
