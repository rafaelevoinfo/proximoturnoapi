using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record TagDTO {
    private string? _nome;
    public int? Id { get; set; }
    public string? Nome { 
        get => _nome; 
        set => _nome = value != null ? StringUtils.Capitalize(value) : null; 
    }

    public static TagDTO FromModel(Tag tag) {
        return new TagDTO {
            Id = tag.Id,
            Nome = tag.Nome,
        };
    }

    public Tag ToModel() {
        return new Tag {
            Id = Id ?? 0,
            Nome = Nome ?? string.Empty,
        };
    }

    public void UpdateModel(Tag tag) {
        if (Nome != null) {
            tag.Nome = Nome;
        }
    }
}