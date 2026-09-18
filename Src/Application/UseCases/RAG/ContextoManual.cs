namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// De que manual de que jogo o texto veio. Serve ao chunking, que precisa disso no
/// caminho de títulos, e ao revisor, que precisa saber o que não pode corrigir.
/// </summary>
public sealed record ContextoManual(string NomeJogo, string TituloManual) {

    public string Prefixo =>
        string.Join(" > ", new[] { NomeJogo, TituloManual }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
}
