using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum JogoStatus : short {
    Disponivel,
    Alugado,
    Indisponivel
}

[Table("JOGO")]
public class Jogo {
    [Column("ID")]
    public int Id { get; set; }

    [Column("ID_CATEGORIA")]
    public int IdCategoria { get; set; }

    [Column("NOME"), MaxLength(100)]
    public required string Nome { get; set; }

    [Column("DESCRICAO")]
    public required string Descricao { get; set; }

    [Column("IDADE_MINIMA")]
    public short IdadeMinima { get; set; }

    [Column("FOTO")]
    public required byte[] Foto { get; set; }

    [Column("MINIMO_JOGADORES")]
    public short MinimoDeJogadores { get; set; }

    [Column("MAXIMO_JOGADORES")]
    public short MaximoDeJogadores { get; set; }

    [Column("STATUS")]
    public JogoStatus Status { get; set; }

    [Column("TEMPO_ESTIMADO_JOGO")]
    public TimeOnly? TempoEstimadoDeJogo { get; set; }

    [Column("VALOR_COMPRA")]
    public decimal? ValorDeCompra { get; set; }

    [Column("DATA_COMPRA")]
    public DateOnly? DataCompra { get; set; }

    public Categoria? Categoria { get; set; }
    public List<Tag>? Tags { get; set; }
    public List<Link>? Links { get; set; }
}