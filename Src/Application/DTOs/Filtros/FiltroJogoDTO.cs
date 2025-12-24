using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroJogoDTO {
    [FromQuery(Name = "nome")]
    public string? Nome { get; set; }
    [FromQuery(Name = "id_categoria")]
    public int? IdCategoria { get; set; }
    [FromQuery(Name = "tags")]
    public List<string>? Tags { get; set; }
    [FromQuery(Name = "status")]
    public StatusJogo? Status { get; set; }
    [FromQuery(Name = "idade_minima")]
    public short? IdadeMinima { get; set; }
    [FromQuery(Name = "numero_de_jogadores")]
    public short? NumeroDeJogadores { get; set; }
}
