namespace ProximoTurnoApi.Application.DTOs;

/// <summary>Um arquivo de log gravado pelo sink de arquivo do Serilog.</summary>
public record ArquivoLogDTO {
    public string Nome { get; init; } = "";
    public long TamanhoBytes { get; init; }
    public DateTime UltimaAlteracao { get; init; }
}

/// <summary>
/// Uma entrada de log. Corresponde a uma linha de cabeçalho do arquivo mais as linhas
/// seguintes que não começam com data (stack trace de exceção, mensagens multilinha).
/// </summary>
public record EntradaLogDTO {
    /// <summary>Arquivo de onde a entrada veio; com Numero, identifica a entrada.</summary>
    public string Arquivo { get; init; } = "";
    /// <summary>Posição da entrada no arquivo (1 = primeira), estável entre consultas.</summary>
    public int Numero { get; init; }
    /// <summary>Horário como gravado no arquivo, com o fuso do servidor.</summary>
    public string? DataHora { get; init; }
    public string? Nivel { get; init; }
    public string? TraceId { get; init; }
    public string? Origem { get; init; }
    public string Mensagem { get; init; } = "";
    /// <summary>Linhas de continuação, normalmente a exceção. Null quando não há.</summary>
    public string? Detalhes { get; init; }
}

public record ConsultaLogsDTO {
    /// <summary>Arquivos lidos, do mais antigo ao mais novo; vazio quando não há log gravado.</summary>
    public IReadOnlyList<string> Arquivos { get; init; } = [];
    /// <summary>Total de entradas dos arquivos lidos que passaram nos filtros.</summary>
    public int TotalEncontrado { get; init; }
    /// <summary>Entradas mais recentes primeiro, limitadas ao pedido.</summary>
    public IReadOnlyList<EntradaLogDTO> Entradas { get; init; } = [];
}

public record FiltroLogs {
    /// <summary>Arquivo específico; tem precedência sobre o período.</summary>
    public string? Arquivo { get; init; }
    /// <summary>Início do período (inclusivo). Sem arquivo, define quais dias são lidos.</summary>
    public DateTimeOffset? Inicio { get; init; }
    /// <summary>Fim do período (inclusivo).</summary>
    public DateTimeOffset? Fim { get; init; }
    /// <summary>Níveis exatos do Serilog (ex.: Warning, Error); vazio traz todos.</summary>
    public IReadOnlyList<string>? Niveis { get; init; }
    /// <summary>
    /// Texto livre, sem diferenciar maiúsculas, procurado em mensagem, exceção e origem:
    /// todas as palavras precisam aparecer, "frases entre aspas" valem inteiras e
    /// -palavra exclui.
    /// </summary>
    public string? Busca { get; init; }
    public string? TraceId { get; init; }
    public int? Limite { get; init; }
}
