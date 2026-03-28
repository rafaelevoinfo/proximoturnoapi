using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;


public record JogoResumoDTO {
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public static JogoResumoDTO FromModel(Jogo jogo) {
        return new JogoResumoDTO {
            Id = jogo.Id,
            Nome = jogo.Nome,
        };
    }
}

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

public record CopiaJogoDTO {
    public int Id { get; set; }
    public StatusJogo Status { get; set; }

    public static CopiaJogoDTO FromModel(JogoCopia copia) {
        return new CopiaJogoDTO() {
            Id = copia.Id,
            Status = copia.Status
        };
    }
}

public record JogoDTO {
    private string _nome = null!;
    public int Id { get; set; }
    [Required]
    public int IdCategoria { get; set; }
    [Required]
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    [Required]
    public string Descricao { get; set; } = string.Empty;
    [Required]
    public short IdadeMinima { get; set; }
    [Required]
    public string Foto { get; set; } = string.Empty;
    [Required]
    public short MinimoDeJogadores { get; set; }
    [Required]
    public short MaximoDeJogadores { get; set; }
    [Required]
    public StatusJogo Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public decimal? ValorDeCompra { get; set; }
    public DateOnly? DataCompra { get; set; }
    public List<TagDTO>? Tags { get; set; }
    public List<LinkDTO>? Links { get; set; }
    public List<CopiaJogoDTO>? Copias { get; set; } = [];

    public static JogoDTO FromModel(Jogo jogo) {
        var result = new JogoDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Descricao = jogo.Descricao,
            IdadeMinima = jogo.IdadeMinima,
            Foto = jogo.Foto ?? string.Empty,
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            ValorDeCompra = jogo.ValorDeCompra,
            DataCompra = jogo.DataCompra,
            Status = StatusJogo.Disponivel,
            Links = jogo.Links?.Select(LinkDTO.FromModel).ToList(),
            Tags = jogo.Tags?.Select(TagDTO.FromModel).ToList(),
            Copias = jogo.Copias?.Select(CopiaJogoDTO.FromModel).ToList()
        };
        if (jogo.Copias is not null) {
            foreach (var copia in jogo.Copias) {
                if (copia.Status == StatusJogo.Disponivel) {
                    result.Status = copia.Status;
                    break;
                } else if (copia.Status > result.Status) {
                    result.Status = copia.Status;
                }
            }
        }
        return result;
    }

    public void UpdateModel(Jogo jogo) {
        jogo.IdCategoria = IdCategoria;
        jogo.Nome = Nome;
        jogo.Descricao = Descricao;
        jogo.IdadeMinima = IdadeMinima;
        jogo.Foto = string.IsNullOrEmpty(Foto) ? jogo.Foto : Foto;
        jogo.MinimoDeJogadores = MinimoDeJogadores;
        jogo.MaximoDeJogadores = MaximoDeJogadores;
        jogo.TempoEstimadoDeJogo = TempoEstimadoDeJogo;
        jogo.ValorDeCompra = ValorDeCompra;
        jogo.DataCompra = DataCompra;
        jogo.Tags = Tags?.Select(tag => tag.ToModel()).ToList();
        if (Links is not null) {
            jogo.Links ??= [];
            foreach (var link in jogo.Links) {
                var linkDto = Links.FirstOrDefault(l => l.Id == link.Id);
                if (linkDto is null) {
                    // Remover links que não estão mais no DTO
                    jogo.Links?.Remove(link);
                } else {
                    // Atualizar links existentes
                    link.Titulo = linkDto.Titulo;
                    link.Url = linkDto.Url;
                }
            }

            foreach (var linkDto in Links) {
                if (!jogo.Links!.Any(l => l.Id == linkDto.Id)) {
                    // Adicionar novos links
                    jogo.Links!.Add(linkDto.ToModel());
                }
            }

        }

    }

    public Jogo ToModel() {
        return new Jogo {
            Id = Id,
            IdCategoria = IdCategoria,
            Nome = Nome,
            Descricao = Descricao,
            IdadeMinima = IdadeMinima,
            Foto = Foto ?? string.Empty,
            MinimoDeJogadores = MinimoDeJogadores,
            MaximoDeJogadores = MaximoDeJogadores,
            TempoEstimadoDeJogo = TempoEstimadoDeJogo,
            ValorDeCompra = ValorDeCompra,
            DataCompra = DataCompra,
            Links = Links?.Select(link => link.ToModel()).ToList(),
            Tags = Tags?.Select(tag => tag.ToModel()).ToList()
        };
    }
}
