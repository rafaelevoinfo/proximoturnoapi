using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoResumoDTO {
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public static JogoResumoDTO FromModel(Jogo jogo) {
        return new JogoResumoDTO {
            Id = jogo.Id,
            Nome = jogo.Nome,
        };
    }
}
