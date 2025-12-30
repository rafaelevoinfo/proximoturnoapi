using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record TagDTO {
    private string _nome = null!;
    public int? Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }

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