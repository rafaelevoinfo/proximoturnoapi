using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

/// <summary>
/// Status de uma cópia. Gravado como número em JOGO_COPIA.STATUS: os valores são explícitos e
/// não podem ser renumerados. O 3 (Indisponivel) nunca foi gravado em cópia e foi removido;
/// a migração RemoveStatusIndisponivel converte para Manutencao qualquer linha que o tivesse.
/// </summary>
public enum StatusJogo : short {
    Disponivel = 0,
    /// <summary>Num pedido ainda não entregue. Só o fluxo de pedidos entra e sai daqui.</summary>
    Reservado = 1,
    /// <summary>Com o cliente. Só o fluxo de pedidos entra e sai daqui.</summary>
    Alugado = 2,
    /// <summary>Cópia excluída. O jogo com todas as cópias assim some do catálogo e do assistente.</summary>
    Desativado = 4,
    /// <summary>Definido pelo admin: não é alugada, mas o jogo segue no catálogo e no assistente de regras.</summary>
    ApenasEmEventos = 5,
    /// <summary>Definido pelo admin: fora do aluguel enquanto é consertada.</summary>
    Manutencao = 6,
}

[Table("JOGO_COPIA")]
public class JogoCopia : BaseModel {
    [Column("ID_JOGO")]
    public int IdJogo { get; set; }
    [Column("STATUS")]
    public StatusJogo Status { get; set; }

    [ForeignKey(nameof(IdJogo))]
    public Jogo? Jogo { get; set; } = null!;
}