using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;


namespace ProximoTurnoApi.Application.DTOs;

public record CategoriaDTO {
    private string _descricao = null!;
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Descricao { get => _descricao; set => _descricao = StringUtils.Capitalize(value); }

    public bool Ativo { get; set; } = true;

    public List<PeriodoDTO> Periodos { get; set; } = [];


    public static CategoriaDTO FromModel(Categoria categoria) {
        return new CategoriaDTO {
            Id = categoria.Id,
            Descricao = StringUtils.Capitalize(categoria.Descricao),
            Ativo = categoria.Ativo,
            Periodos = categoria.Periodos.Select(p => new PeriodoDTO() {
                Id = p.Id,
                QtdeDias = p.QuantidadeDias,
                Valor = p.Valor
            }).ToList()
        };
    }

}

public record PeriodoDTO {
    public int Id { get; set; }
    public int QtdeDias { get; set; }
    public decimal Valor { get; set; }
}
