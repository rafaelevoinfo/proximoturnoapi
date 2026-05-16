using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public class JogoMaisAlugado {
    [Column("ID")]
    public int Id { get; set; }
    [Column("NOME")]
    public string Nome { get; set; } = null!;
    [Column("QTDE")]
    public int Qtde { get; set; }
    [Column("FOTO")]
    public string? Foto { get; set; }
}

[Table("JOGO")]
public class Jogo : BaseModel {
    [Column("ID_CATEGORIA")]
    public int IdCategoria { get; set; }

    [Column("NOME"), MaxLength(100)]
    public string Nome { get; set; } = null!;

    [Column("DESCRICAO")]
    public string Descricao { get; set; } = null!;

    [Column("IDADE_MINIMA")]
    public short IdadeMinima { get; set; }

    [Column("MINIMO_JOGADORES")]
    public short MinimoDeJogadores { get; set; }

    [Column("MAXIMO_JOGADORES")]
    public short MaximoDeJogadores { get; set; }

    [Column("COMPLEXIDADE")]
    public decimal? Complexidade { get; set; }

    [Column("TEMPO_ESTIMADO_JOGO")]
    public TimeOnly? TempoEstimadoDeJogo { get; set; }

    [Column("VALOR_COMPRA")]
    public decimal? ValorDeCompra { get; set; }

    [Column("DATA_COMPRA")]
    public DateOnly? DataCompra { get; set; }

    public Categoria? Categoria { get; set; }
    public List<Tag>? Tags { get; set; }
    public List<JogoLink>? Links { get; set; }
    public List<JogoFoto>? Fotos { get; set; }
    public List<JogoCopia>? Copias { get; set; }
}
