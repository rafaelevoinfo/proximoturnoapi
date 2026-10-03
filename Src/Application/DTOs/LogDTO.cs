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
    /// <summary>Arquivo consultado; null quando ainda não há nenhum log gravado.</summary>
    public string? Arquivo { get; init; }
    /// <summary>Total de entradas do arquivo que passaram nos filtros.</summary>
    public int TotalEncontrado { get; init; }
    /// <summary>Entradas mais recentes primeiro, limitadas ao pedido.</summary>
    public IReadOnlyList<EntradaLogDTO> Entradas { get; init; } = [];
}
