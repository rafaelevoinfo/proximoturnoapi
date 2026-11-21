using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.DTOs;

public record JogoCardDTO {
    private string _nome = null!;
    public int Id { get; set; }
    public int IdCategoria { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    public string Foto { get; set; } = string.Empty;
    public short MinimoDeJogadores { get; set; }
    public short MaximoDeJogadores { get; set; }
    public short IdadeMinima { get; set; }
    public decimal? Complexidade { get; set; }
    public StatusJogo Status { get; set; }
    public TimeOnly? TempoEstimadoDeJogo { get; set; }
    public int TotalCopias { get; set; }
    public int CopiasDisponiveis { get; set; }
    /// <summary>Só na listagem do admin: se o assistente de regras tem o manual do jogo.</summary>
    public SituacaoManual? Manual { get; set; }
    /// <summary>Só na listagem do admin: os links de regra indexados (manual, FAQ...), para abrir o markdown gerado.</summary>
    public List<ManualIndexadoDTO>? ManuaisIndexados { get; set; }

    public static JogoCardDTO FromModel(Jogo jogo) {
        var result = new JogoCardDTO {
            Id = jogo.Id,
            IdCategoria = jogo.IdCategoria,
            CategoriaNome = StringUtils.Capitalize(jogo.Categoria?.Descricao ?? string.Empty),
            Nome = jogo.Nome,
            Foto = jogo.Fotos?.OrderBy(f => f.Ordem).FirstOrDefault()?.Url ?? string.Empty,
            MinimoDeJogadores = jogo.MinimoDeJogadores,
            MaximoDeJogadores = jogo.MaximoDeJogadores,
            IdadeMinima = jogo.IdadeMinima,
            Complexidade = jogo.Complexidade,
            TempoEstimadoDeJogo = jogo.TempoEstimadoDeJogo,
            // Sem cópia nenhuma não há o que alugar.
            Status = StatusJogo.Desativado,
            TotalCopias = jogo.Copias?.Count(c => c.Status != StatusJogo.Desativado) ?? 0,
            CopiasDisponiveis = jogo.Copias?.Count(c => c.Status == StatusJogo.Disponivel) ?? 0
        };

        result.Status = StatusDoJogo.Calcular(jogo.Copias) ?? result.Status;

        return result;
    }

    /// <summary>Desconta as cópias que outros carrinhos seguram: o visitante vê o que de fato pode alugar.</summary>
    public JogoCardDTO AplicarReservasCarrinho(IReservasCarrinho reservas, Guid? idCarrinho) {
        var seguradas = reservas.SeguradasPorOutros(Id, idCarrinho);
        if (seguradas > 0) {
            Status = StatusDoJogo.ComReservasDeCarrinho(Status, CopiasDisponiveis, seguradas);
            CopiasDisponiveis = Math.Max(0, CopiasDisponiveis - seguradas);
        }
        return this;
    }
}

public record ManualIndexadoDTO(int IdJogoLink, string Titulo);
