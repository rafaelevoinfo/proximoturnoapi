using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record LinkDTO {
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Url { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)]
    public string Titulo { get; set; } = string.Empty;

    public static LinkDTO FromModel(Link link) {
        return new LinkDTO {
            Id = link.Id,
            Url = link.Url,
            Titulo = link.Titulo,
        };
    }

    public Link ToModel() {
        return new Link {
            Id = Id,
            Url = Url,
            Titulo = Titulo,
        };
    }
}