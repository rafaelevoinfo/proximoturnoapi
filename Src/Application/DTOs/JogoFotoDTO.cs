using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoFotoDTO {
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Ordem { get; set; }

    public static JogoFotoDTO FromModel(JogoFoto foto) {
        return new JogoFotoDTO {
            Id = foto.Id,
            Url = foto.Url,
            Ordem = foto.Ordem
        };
    }

    public JogoFoto ToModel() {
        return new JogoFoto {
            Id = Id,
            Url = Url,
            Ordem = Ordem
        };
    }
}
