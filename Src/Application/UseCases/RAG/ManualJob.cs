namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Manual pendente de extração. Carrega só o necessário para o worker trabalhar,
/// evitando que uma entidade rastreada pelo EF atravesse escopos.
/// </summary>
public record ManualJob(int IdJogoLink, int IdJogo, string Url);
