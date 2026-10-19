using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

/// <summary>
/// Um navegador (celular ou computador) de um usuário que aceitou receber notificações push.
/// Os campos são os da <c>PushSubscription</c> do navegador: o endpoint do serviço de push
/// (FCM, Mozilla, Apple) e as chaves com que a mensagem é cifrada para aquele aparelho.
/// </summary>
[Table("INSCRICAO_PUSH")]
public class InscricaoPush : BaseModel {

    [Column("ID_USUARIO"), MaxLength(255)]
    public required string IdUsuario { get; set; }

    /// <summary>URL única por navegador: é por ela que se acha a inscrição de novo.</summary>
    [Column("ENDPOINT"), MaxLength(600)]
    public required string Endpoint { get; set; }

    [Column("P256DH"), MaxLength(200)]
    public required string P256dh { get; set; }

    [Column("AUTH"), MaxLength(100)]
    public required string Auth { get; set; }

    /// <summary>Só para saber de que aparelho é a inscrição ao olhar o banco.</summary>
    [Column("USER_AGENT"), MaxLength(300)]
    public string? UserAgent { get; set; }

    [Column("DATA_CRIACAO")]
    public DateTime DataCriacao { get; set; }
}
