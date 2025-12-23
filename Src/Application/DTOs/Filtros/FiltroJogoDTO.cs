using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroJogoDTO {
    public string? Nome { get; set; }
    public int? IdCategoria { get; set; }
    public List<string>? Tags { get; set; }
    public JogoStatus? Status { get; set; }
    public short? IdadeMinima { get; set; }
    public short? NumeroDeJogadores { get; set; }
}
