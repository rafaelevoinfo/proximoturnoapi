using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoPublicDTO {
    protected string _nome = null!;
    public int Id { get; set; }
    public int IdCategoria { get; set; }
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    public string Descricao { get; set; } = string.Empty;
    public short IdadeMinima { get; set; }
    public short MinimoDeJogadores { get; set; }
    public short MaximoDeJogadores { get; set; }
    public decimal? Complexidade { get; set; }
    public StatusJogo Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public List<TagDTO>? Tags { get; set; }
    public List<JogoLinkDTO>? Links { get; set; }
    public List<JogoFotoDTO>? Fotos { get; set; }
    public List<CopiaJogoDTO>? Copias { get; set; } = [];

    public static JogoPublicDTO FromModel(Jogo jogo) {
        var result = new JogoPublicDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            Nome = jogo.Nome,
            Descricao = jogo.Descricao,
            IdadeMinima = jogo.IdadeMinima,
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            Complexidade = jogo.Complexidade,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
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
}
