using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

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
    public short MaximoDeJogadores { get; set; }
    public decimal? Complexidade { get; set; }
    [Required]
    public StatusJogo Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public decimal? ValorDeCompra { get; set; }
    public DateOnly? DataCompra { get; set; }
    public List<TagDTO>? Tags { get; set; }
    public List<LinkDTO>? Links { get; set; }
    public List<JogoFotoDTO>? Fotos { get; set; }
    public List<CopiaJogoDTO>? Copias { get; set; } = [];

    public static JogoDTO FromModel(Jogo jogo) {
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
                result.Status = jogo.Copias.Min(c => c.Status);
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
