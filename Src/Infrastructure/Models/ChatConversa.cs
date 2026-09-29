using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Infrastructure.Models;

/// <summary>
/// Uma conversa do chat de regras: um usuário e a sessão do agente (MAF) serializada, que é a
/// memória do modelo entre uma pergunta e outra. A conversa pode passar por vários jogos: quem
/// escolhe o jogo de cada busca é o modelo, pelas ferramentas.
/// </summary>
[Table("CHAT_CONVERSA")]
public class ChatConversa : BaseModel {

    /// <summary>Identificador público, o que o front guarda. O ID inteiro não sai da API.</summary>
    [Column("CHAVE")]
    public Guid Chave { get; set; }

    [Column("ID_USUARIO"), MaxLength(255)]
    public required string IdUsuario { get; set; }

    /// <summary>Jogo da página em que a conversa começou, se houver. Só informativo.</summary>
    [Column("ID_JOGO")]
    public int? IdJogo { get; set; }

    /// <summary>JSON do <c>AgentSession</c>: o histórico já reduzido que vai ao modelo.</summary>
    [Column("SESSAO")]
    public string? Sessao { get; set; }

    [Column("DATA_CRIACAO")]
    public DateTime DataCriacao { get; set; }

    [Column("DATA_ATUALIZACAO")]
    public DateTime DataAtualizacao { get; set; }

    public List<ChatMensagem> Mensagens { get; set; } = [];
}

/// <summary>
/// Uma pergunta e o que a API fez com ela, completa e sem redução: diferente da sessão, que o
/// redutor resume, isto é o registro fiel da conversa, incluindo os trechos que a busca achou.
/// </summary>
[Table("CHAT_MENSAGEM")]
public class ChatMensagem : BaseModel {

    [Column("ID_CONVERSA")]
    public int IdConversa { get; set; }

    [Column("MOMENTO")]
    public DateTime Momento { get; set; }

    [Column("PERGUNTA")]
    public required string Pergunta { get; set; }

    [Column("RESPOSTA")]
    public string? Resposta { get; set; }

    [Column("TIPO")]
    public TipoRespostaChat Tipo { get; set; }

    /// <summary>JSON dos trechos do manual devolvidos pela busca vetorial, com o score de cada um.</summary>
    [Column("TRECHOS")]
    public string? Trechos { get; set; }
}
