namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Resultado da revisão. <see cref="Modelo"/> nulo significa que nenhum bloco chegou a ser
/// revisado: nesse caso o texto devolvido é o original e não vale como cache.
/// </summary>
public sealed record ResultadoRevisao(string Texto, string? Modelo, int Aplicadas, int Descartadas, bool Completa);

public interface IRevisorMarkdown {
    Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken cancellationToken);
}
