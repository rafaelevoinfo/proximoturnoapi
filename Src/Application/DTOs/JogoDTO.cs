using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoDTO {
    public int Id { get; set; }
    [Required]
    public int IdCategoria { get; set; }
    [Required]
    public string Nome { get; set => field = StringUtils.Capitalize(value); } = string.Empty;
    [Required]
    public string Descricao { get; set; } = string.Empty;
    [Required]
    public short IdadeMinima { get; set; }
    [Required]
    public byte[] Foto { get; set; } = null!;
    [Required]
    public short MinimoDeJogadores { get; set; }
    [Required]
    public short MaximoDeJogadores { get; set; }
    [Required]
    public JogoStatus Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public decimal? ValorDeCompra { get; set; }
    public DateOnly? DataCompra { get; set; }
    public List<TagDTO>? Tags { get; set; }
    public List<LinkDTO>? Links { get; set; }

    public static JogoDTO FromModel(Jogo jogo) {
        return new JogoDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Descricao = jogo.Descricao,
            IdadeMinima = jogo.IdadeMinima,
            Foto = jogo.Foto ?? [],
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            Status = jogo.Status,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            ValorDeCompra = jogo.ValorDeCompra,
            DataCompra = jogo.DataCompra,
            Links = jogo.Links?.Select(LinkDTO.FromModel).ToList(),
            Tags = jogo.Tags?.Select(tag => TagDTO.FromModel(tag)).ToList()
        };
    }

    public void UpdateModel(Jogo jogo) {
        jogo.IdCategoria = IdCategoria;
        jogo.Nome = Nome;
        jogo.Descricao = Descricao;
        jogo.IdadeMinima = IdadeMinima;
        jogo.Foto = Foto ?? jogo.Foto;
        jogo.MinimoDeJogadores = MinimoDeJogadores;
        jogo.MaximoDeJogadores = MaximoDeJogadores;
        jogo.Status = Status;
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
            Foto = Foto ?? [],
            MinimoDeJogadores = MinimoDeJogadores,
            MaximoDeJogadores = MaximoDeJogadores,
            Status = Status,
            TempoEstimadoDeJogo = TempoEstimadoDeJogo,
            ValorDeCompra = ValorDeCompra,
            DataCompra = DataCompra,
            Links = Links?.Select(link => link.ToModel()).ToList(),
            Tags = Tags?.Select(tag => tag.ToModel()).ToList()
        };
    }
}
