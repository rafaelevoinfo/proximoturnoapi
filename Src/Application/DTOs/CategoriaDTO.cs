using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;


namespace ProximoTurnoApi.Application.DTOs;

public record CategoriaDTO {
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Descricao { get; set => field = StringUtils.Capitalize(value); } = string.Empty;

    public List<int> FaixasPrecoIds { get; set; } = [];

    public Categoria ToModel() {
        return new Categoria {
            Id = Id ?? 0,
            Descricao = Descricao,
        };
    }

    public void UpdateModel(Categoria categoria) {
        categoria.Descricao = Descricao;
    }

    public static CategoriaDTO FromModel(Categoria categoria) {
        return new CategoriaDTO {
            Id = categoria.Id,
            Descricao = StringUtils.Capitalize(categoria.Descricao),
            FaixasPrecoIds = categoria.FaixasPreco.Select(fp => fp.Id).ToList()
        };
    }
}
