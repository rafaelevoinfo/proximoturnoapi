using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoMaisAlugadoDTO {
    private string _nome = null!;
    public int Qtde { get; set; }
    public int Id { get; set; }
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    public string Foto { get; set; } = string.Empty;

    public static JogoMaisAlugadoDTO FromModel(JogoMaisAlugado jogo) {
        return new JogoMaisAlugadoDTO {
            Id = jogo.Id,
            Nome = jogo.Nome,
            Foto = jogo.Foto ?? string.Empty
        };
    }
}
