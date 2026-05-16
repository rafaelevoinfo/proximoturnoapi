using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoDTO : JogoPublicDTO {
    public int QuantidadeCopias { get; set; } = 1;
    public decimal? ValorDeCompra { get; set; }
    public DateOnly? DataCompra { get; set; }

    public new static JogoDTO FromModel(Jogo jogo) {
        var result = new JogoDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Descricao = jogo.Descricao,
            IdadeMinima = jogo.IdadeMinima,
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            Complexidade = jogo.Complexidade,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            ValorDeCompra = jogo.ValorDeCompra,
            DataCompra = jogo.DataCompra,
            Status = StatusJogo.Disponivel,
            Links = jogo.Links?.Select(LinkDTO.FromModel).ToList(),
            Tags = jogo.Tags?.Select(TagDTO.FromModel).ToList(),
            Fotos = jogo.Fotos?.Select(JogoFotoDTO.FromModel).OrderBy(f => f.Ordem).ToList(),
            Copias = jogo.Copias?.Select(CopiaJogoDTO.FromModel).ToList()
        };
        if (jogo.Copias is not null && jogo.Copias.Any()) {
            if (jogo.Copias.Any(c => c.Status == StatusJogo.Disponivel)) {
                result.Status = StatusJogo.Disponivel;
            } else {
                result.Status = jogo.Copias.Where(c => c.Status != StatusJogo.Desativado).Min(c => (StatusJogo?)c.Status) ?? StatusJogo.Indisponivel;
            }
        }
        return result;
    }

    public void UpdateModel(Jogo jogo) {
        jogo.IdCategoria = IdCategoria;
        jogo.Nome = Nome;
        jogo.Descricao = Descricao;
        jogo.IdadeMinima = IdadeMinima;
        jogo.MinimoDeJogadores = MinimoDeJogadores;
        jogo.MaximoDeJogadores = MaximoDeJogadores;
        jogo.Complexidade = Complexidade;
        jogo.TempoEstimadoDeJogo = TempoEstimadoDeJogo;
        jogo.ValorDeCompra = ValorDeCompra;
        jogo.DataCompra = DataCompra;
        jogo.Tags = Tags?.Select(tag => tag.ToModel()).ToList();

        if (Fotos is not null) {
            jogo.Fotos = Fotos.Select(f => f.ToModel()).ToList();
        }

        if (Links is not null) {
            jogo.Links ??= [];
            foreach (var link in jogo.Links.ToList()) {
                var linkDto = Links.FirstOrDefault(l => l.Id == link.Id);
                if (linkDto is null) {
                    jogo.Links.Remove(link);
                } else {
                    link.Titulo = linkDto.Titulo;
                    link.Url = linkDto.Url;
                    link.Tipo = linkDto.Tipo;
                }
            }

            foreach (var linkDto in Links) {
                if (!jogo.Links!.Any(l => l.Id == linkDto.Id)) {
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
            MinimoDeJogadores = MinimoDeJogadores,
            MaximoDeJogadores = MaximoDeJogadores,
            Complexidade = Complexidade,
            TempoEstimadoDeJogo = TempoEstimadoDeJogo,
            ValorDeCompra = ValorDeCompra,
            DataCompra = DataCompra,
            Links = Links?.Select(link => link.ToModel()).ToList(),
            Tags = Tags?.Select(tag => tag.ToModel()).ToList(),
            Fotos = Fotos?.Select(foto => foto.ToModel()).ToList()
        };
    }
}
