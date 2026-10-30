using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

/// <summary>
/// Configurações gerais da loja, numa única linha. Hoje só guarda se a loja está fechada:
/// fechada, o catálogo mostra o aviso e os clientes não conseguem fazer pedidos.
/// </summary>
[Table("CONFIGURACAO_LOJA")]
public class ConfiguracaoLoja : BaseModel {
    [Column("LOJA_FECHADA")]
    public bool LojaFechada { get; set; }

    /// <summary>Aviso exibido no catálogo; vazio usa a mensagem padrão.</summary>
    [Column("MENSAGEM_LOJA_FECHADA"), MaxLength(500)]
    public string? MensagemLojaFechada { get; set; }

    [Column("DATA_HORA_ALTERACAO")]
    public DateTime DataHoraAlteracao { get; set; }
}
