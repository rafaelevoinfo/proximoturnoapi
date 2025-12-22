namespace ProximoTurnoApi.Models;

public class Jogo {
    public int Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int IdadeMinima { get; set; }
    public required byte[] Foto { get; set; }
    public int MinimoDeJogadores { get; set; }
    public int MaximoDeJogadores { get; set; }
    public required int IdCategoria { get; set; }
    public required string Tags { get; set; }
    public string? TempoEstimadoDeJogo { get; set; }
    public string? LinksParaVideosSobreOJogo { get; set; }
    public decimal? ValorDeCompra { get; set; }
    public DateTime? DataDeAcquisicao { get; set; }
}