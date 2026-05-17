using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoLinkDTO {
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Url { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)]
    public string Titulo { get; set; } = string.Empty;
    public TipoLink Tipo { get; set; }

    public static JogoLinkDTO FromModel(JogoLink link) {
        return new JogoLinkDTO {
            Id = link.Id,
            Url = link.Url,
            Titulo = link.Titulo,
            Tipo = link.Tipo
        };
    }

    public JogoLink ToModel() {
        return new JogoLink {
            Id = Id,
            Url = Url,
            Titulo = Titulo,
            Tipo = Tipo
        };
    }
}