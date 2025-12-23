using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record TagDTO {
    public int? Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public string Nome { get; set => field = StringUtils.Capitalize(value); } = string.Empty;

    public static TagDTO FromModel(Tag tag) {
        return new TagDTO {
            Id = tag.Id,
            Nome = tag.Nome,
        };
    }

    public Tag ToModel() {
        return new Tag {
            Id = Id ?? 0,
            Nome = Nome,
        };
    }

    public void UpdateModel(Tag tag) {
        tag.Nome = Nome;
    }
}