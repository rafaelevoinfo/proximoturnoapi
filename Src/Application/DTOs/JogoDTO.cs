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
            Links = jogo.Links?.Select(JogoLinkDTO.FromModel).ToList(),
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

    public void UpdateModel(Jogo model) {
        model.IdCategoria = IdCategoria;
        model.Nome = Nome;
        model.Descricao = Descricao;
        model.IdadeMinima = IdadeMinima;
        model.MinimoDeJogadores = MinimoDeJogadores;
        model.MaximoDeJogadores = MaximoDeJogadores;
        model.Complexidade = Complexidade;
        model.TempoEstimadoDeJogo = TempoEstimadoDeJogo;
        model.ValorDeCompra = ValorDeCompra;
        model.DataCompra = DataCompra;

        if (Links is not null) {
            model.Links ??= [];
            foreach (var link in model.Links.ToList()) {
                var linkDto = Links.FirstOrDefault(l => l.Id == link.Id);
                if (linkDto is null) {
                    model.Links.Remove(link);
                } else {
                    link.Titulo = linkDto.Titulo;
                    link.Url = linkDto.Url;
                    link.Tipo = linkDto.Tipo;
                }
            }

            foreach (var linkDto in Links) {
                if (linkDto.Id == 0) {
                    model.Links.Add(linkDto.ToModel());
                }
            }
        } else {
            model.Links?.Clear();
        }

        if (Fotos is not null && Fotos.Count > 0) {
            model.Fotos ??= [];
            foreach (var foto in model.Fotos.ToList()) {
                var fotoDto = Fotos.FirstOrDefault(f => f.Id == foto.Id);
                if (fotoDto is null) {
                    model.Fotos.Remove(foto);
                } else {
                    foto.Url = fotoDto.Url;
                    foto.Ordem = fotoDto.Ordem;
                }
            }

            foreach (var fotoDto in Fotos) {
                if (fotoDto.Id == 0) {
                    model.Fotos.Add(fotoDto.ToModel());
                }
            }
        } else {
            model.Fotos?.Clear();
        }
    }

    public Jogo ToModel() {
        var jogo = new Jogo {
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
            Fotos = Fotos?.Select(foto => foto.ToModel()).ToList()
        };
        var qtdeCopias = QuantidadeCopias > 0 ? QuantidadeCopias : 1;

        if (qtdeCopias != Copias?.Count) {
            jogo.Copias = [];
            for (int i = 0; i < qtdeCopias; i++) {
                jogo.Copias.Add(new JogoCopia() {
                    Status = StatusJogo.Disponivel
                });
            }
        }
        return jogo;
    }
}
