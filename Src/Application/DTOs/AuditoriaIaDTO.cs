using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record PaginaDTO<T> {
    public IReadOnlyList<T> Itens { get; init; } = [];
    public int Total { get; init; }
    public int Pagina { get; init; }
    public int Tamanho { get; init; }
}

// ---- USO_LLM: uma linha por requisição ----

public enum OrdemUsoLlm {
    Recentes,
    MaiorCusto,
    MaisLentas,
}

public record FiltroUsoLlm {
    public DateOnly? DataInicial { get; init; }
    public DateOnly? DataFinal { get; init; }
    public OperacaoLlm? Operacao { get; init; }
    public DesfechoLlm? Desfecho { get; init; }
    public string? Modelo { get; init; }
    /// <summary>Procura no e-mail e no nome do usuário e no alvo (jogo / manual).</summary>
    public string? Busca { get; init; }
    public string? TraceId { get; init; }
    public OrdemUsoLlm Ordem { get; init; } = OrdemUsoLlm.Recentes;
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 50;
}

public record UsoLlmDTO {
    public int Id { get; init; }
    public DateTime Momento { get; init; }
    public OperacaoLlm Operacao { get; init; }
    public string OperacaoDescricao { get; init; } = "";
    public DesfechoLlm Desfecho { get; init; }
    public string DesfechoDescricao { get; init; } = "";
    public string ModeloPedido { get; init; } = "";
    public string? ModeloRespondeu { get; init; }
    public string? Provider { get; init; }
    public string? Alvo { get; init; }
    public int? IdJogo { get; init; }
    public string? IdUsuario { get; init; }
    public string? UsuarioEmail { get; init; }
    public string? UsuarioNome { get; init; }
    public int TokensEntrada { get; init; }
    public int TokensSaida { get; init; }
    public int TokensRaciocinio { get; init; }
    public int TokensCache { get; init; }
    public decimal? CustoUsd { get; init; }
    public decimal? CustoBrl { get; init; }
    public decimal? CotacaoUsdBrl { get; init; }
    public int DuracaoMs { get; init; }
    public string? Detalhe { get; init; }
    public string? TraceId { get; init; }
    public string? IdGeracao { get; init; }
}

/// <summary>Totais de todas as requisições que passaram no filtro, não só da página.</summary>
public record ResumoUsoLlmDTO {
    public int Requisicoes { get; init; }
    public int RequisicoesComFalha { get; init; }
    public decimal CustoUsd { get; init; }
    public decimal CustoBrl { get; init; }
    public long DuracaoTotalMs { get; init; }
}

public record ConsultaUsoLlmDTO {
    public PaginaDTO<UsoLlmDTO> Pagina { get; init; } = new();
    public ResumoUsoLlmDTO Resumo { get; init; } = new();
}

// ---- CHAT_CONVERSA / CHAT_MENSAGEM: auditoria do chat de regras ----

public record FiltroConversasChat {
    public DateOnly? DataInicial { get; init; }
    public DateOnly? DataFinal { get; init; }
    /// <summary>E-mail ou nome do usuário.</summary>
    public string? Usuario { get; init; }
    /// <summary>Texto procurado nas perguntas e respostas.</summary>
    public string? Busca { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 30;
}

public record ConversaChatResumoDTO {
    public Guid Chave { get; init; }
    public string IdUsuario { get; init; } = "";
    public string? UsuarioEmail { get; init; }
    public string? UsuarioNome { get; init; }
    public int? IdJogo { get; init; }
    public string? NomeJogo { get; init; }
    public DateTime DataCriacao { get; init; }
    public DateTime DataAtualizacao { get; init; }
    public int QuantidadeMensagens { get; init; }
    public string? PrimeiraPergunta { get; init; }
}

public record ConversaChatDTO {
    public ConversaChatResumoDTO Conversa { get; init; } = new();
    public IReadOnlyList<MensagemChatAuditoriaDTO> Mensagens { get; init; } = [];
}

public record MensagemChatAuditoriaDTO {
    public DateTime Momento { get; init; }
    public string Pergunta { get; init; } = "";
    public string? Resposta { get; init; }
    public TipoRespostaChat Tipo { get; init; }
    /// <summary>Buscas que o modelo fez para responder, na ordem em que fez.</summary>
    public IReadOnlyList<ChamadaFerramentaAuditoriaDTO> Chamadas { get; init; } = [];
    /// <summary>CHAT_MENSAGEM.TRECHOS que não deu para interpretar; vai cru para não sumir.</summary>
    public string? TrechosBrutos { get; init; }
}

public record ChamadaFerramentaAuditoriaDTO {
    /// <summary>Nome da ferramenta; null nos registros anteriores às ferramentas, quando a busca era automática.</summary>
    public string? Ferramenta { get; init; }
    public int? IdJogo { get; init; }
    public string? NomeJogo { get; init; }
    public string? Consulta { get; init; }
    public string? Erro { get; init; }
    public IReadOnlyList<TrechoAuditoriaDTO> Trechos { get; init; } = [];
}

public record TrechoAuditoriaDTO {
    public int IdJogoLink { get; init; }
    /// <summary>Título do manual (JOGO_LINK.TITULO), quando o link ainda existe.</summary>
    public string? Manual { get; init; }
    /// <summary>Título da seção do manual onde o trecho está.</summary>
    public string? Titulo { get; init; }
    public string Texto { get; init; } = "";
    public float Score { get; init; }
    /// <summary>Passou do score mínimo e foi para o modelo. Null nos registros que não guardavam isso.</summary>
    public bool? Usado { get; init; }
}
