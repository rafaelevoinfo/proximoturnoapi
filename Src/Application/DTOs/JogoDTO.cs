using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class JogoDTO {
    public int Id { get; set; }
    public int IdCategoria { get; set; }
    public string Nome { get; set => field = StringUtils.Capitalize(value); } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public short IdadeMinima { get; set; }
    public byte[] Foto { get; set; } = null!;
    public short MinimoDeJogadores { get; set; }
    public short MaximoDeJogadores { get; set; }
    public JogoStatus Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public decimal? ValorDeCompra { get; set; }
    public DateOnly? DataCompra { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Links { get; set; }

    public static JogoDTO FromModel(Jogo jogo) {
        return new JogoDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Descricao = jogo.Descricao,
            IdadeMinima = jogo.IdadeMinima,
            Foto = jogo.Foto ?? [],
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            Status = jogo.Status,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            ValorDeCompra = jogo.ValorDeCompra,
            DataCompra = jogo.DataCompra
        };
    }

    public void UpdateModel(Jogo jogo) {
        jogo.IdCategoria = IdCategoria;
        jogo.Nome = Nome;
        jogo.Descricao = Descricao;
        jogo.IdadeMinima = IdadeMinima;
        jogo.Foto = Foto ?? jogo.Foto;
        jogo.MinimoDeJogadores = MinimoDeJogadores;
        jogo.MaximoDeJogadores = MaximoDeJogadores;
        jogo.Status = Status;
        jogo.TempoEstimadoDeJogo = TempoEstimadoDeJogo;
        jogo.ValorDeCompra = ValorDeCompra;
        jogo.DataCompra = DataCompra;
    }

    public Jogo ToModel() {
        return new Jogo {
            Id = Id,
            IdCategoria = IdCategoria,
            Nome = Nome,
            Descricao = Descricao,
            IdadeMinima = IdadeMinima,
            Foto = Foto ?? [],
            MinimoDeJogadores = MinimoDeJogadores,
            MaximoDeJogadores = MaximoDeJogadores,
            Status = Status,
            TempoEstimadoDeJogo = TempoEstimadoDeJogo,
            ValorDeCompra = ValorDeCompra,
            DataCompra = DataCompra
        };
    }
}
