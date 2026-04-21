using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoCardDTO {
    private string _nome = null!;
    public int Id { get; set; }
    public int IdCategoria { get; set; }
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    public string Foto { get; set; } = string.Empty;
    public short MinimoDeJogadores { get; set; }
    public short MaximoDeJogadores { get; set; }
    public short IdadeMinima { get; set; }
    public decimal? Complexidade { get; set; }
    public StatusJogo Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }

    public static JogoCardDTO FromModel(Jogo jogo) {
        var result = new JogoCardDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Foto = jogo.Fotos?.OrderBy(f => f.Ordem).FirstOrDefault()?.Url ?? string.Empty,
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            IdadeMinima = jogo.IdadeMinima,
            Complexidade = jogo.Complexidade,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            Status = StatusJogo.Indisponivel
        };

        if (jogo.Copias is not null && jogo.Copias.Any()) {
            if (jogo.Copias.Any(c => c.Status == StatusJogo.Disponivel)) {
                result.Status = StatusJogo.Disponivel;
            } else {
                result.Status = jogo.Copias.Min(c => c.Status);
            }
        }

        return result;
    }
}
