using System.Text.Json.Serialization;
using ProximoTurnoApi.Application.UseCases.IA;

namespace ProximoTurnoApi.Application.DTOs;

public record PerguntaChatDTO {
    /// <summary>
    /// Conversa em andamento, devolvida pela resposta anterior. A memória fica na API: o front
    /// não manda histórico.
    /// </summary>
    public Guid? IdConversa { get; set; }

    /// <summary>Jogo que o usuário já confirmou nesta conversa.</summary>
    public int? IdJogoConfirmado { get; set; }

    /// <summary>Jogo da página de onde o chat foi aberto, se houver.</summary>
    public int? IdJogoPagina { get; set; }

    public string Mensagem { get; set; } = "";
}

// Texto em vez de numero so nos enums do chat: a API nao tem conversor global, e liga-lo
// mudaria o contrato de todos os outros endpoints.
[JsonConverter(typeof(JsonStringEnumConverter<TipoRespostaChat>))]
public enum TipoRespostaChat {
    Resposta,
    ConfirmarJogo,
    PerguntarJogo,
    ForaDeEscopo,
    SaldoEsgotado,
    SemManual
}

public record JogoChatDTO(int Id, string Nome);

public record FonteChatDTO(int IdJogoLink, string Titulo);

/// <summary>
/// Resposta do chat. Não carrega saldo de propósito: o crédito do usuário só é visível
/// para admin (<see cref="SaldoChatDTO"/>).
/// </summary>
public record RespostaChatDTO {
    public TipoRespostaChat Tipo { get; init; }
    public string Texto { get; init; } = "";

    /// <summary>Conversa a que a resposta pertence. O front manda de volta na próxima pergunta.</summary>
    public Guid? IdConversa { get; init; }

    /// <summary>Jogo a que a resposta se refere. O front guarda como jogo confirmado.</summary>
    public JogoChatDTO? Jogo { get; init; }

    /// <summary>Só em <see cref="TipoRespostaChat.ConfirmarJogo"/>.</summary>
    public List<JogoChatDTO> OpcoesJogo { get; init; } = [];

    public List<FonteChatDTO> Fontes { get; init; } = [];
}

/// <summary>Crédito do chat de um usuário. Exposto só para admin.</summary>
public record SaldoChatDTO {
    public decimal ValorBaseAlugado { get; init; }
    public decimal CreditoAlugueis { get; init; }
    public decimal Bonus { get; init; }
    public decimal Gasto { get; init; }
    public decimal Saldo { get; init; }
    public bool Ilimitado { get; init; }
    public decimal CotacaoUsdBrl { get; init; }
    public FonteCotacao FonteCotacao { get; init; }
}
