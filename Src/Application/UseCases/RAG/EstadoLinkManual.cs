using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Retrato do link no instante da sincronização. Vem em uma consulta só porque a decisão
/// do <see cref="SincronizarManual"/> depende de todos estes campos ao mesmo tempo.
/// </summary>
public sealed record EstadoLinkManual(
    int IdJogoLink,
    int IdJogo,
    TipoLink Tipo,
    string Url,
    string TituloLink,
    string NomeJogo,
    bool JogoAtivo,
    JogoLinkIndexacao? Indexacao);
