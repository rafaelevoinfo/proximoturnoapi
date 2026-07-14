using Microsoft.Extensions.DependencyInjection;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Infrastructure.Services;

public record CategoriaPeriodoInfo(
    int IdPeriodo,
    int QuantidadeDias,
    decimal Valor,
    int IdCategoria,
    string DescricaoCategoria);

public interface ICategoriaPeriodoCache {
    bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info);
    int GetQuantidadeDias(int idPeriodo, int defaultDias = 1);
    Task RefreshAsync();
}

public class CategoriaPeriodoCache(
    IServiceScopeFactory scopeFactory,
    ILogger<CategoriaPeriodoCache> logger) : ICategoriaPeriodoCache {

    private volatile IReadOnlyDictionary<int, CategoriaPeriodoInfo> _porPeriodo =
        new Dictionary<int, CategoriaPeriodoInfo>();

    public void AtualizarCache(IEnumerable<CategoriaPeriodoInfo> periodos) {
        _porPeriodo = periodos.ToDictionary(p => p.IdPeriodo);
    }

    public bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info) =>
        _porPeriodo.TryGetValue(idPeriodo, out info);

    public int GetQuantidadeDias(int idPeriodo, int defaultDias = 1) {
        if (_porPeriodo.TryGetValue(idPeriodo, out var info)) {
            return info.QuantidadeDias;
        }
        logger.LogWarning("Período {IdPeriodo} não encontrado no cache; usando {Default} dia(s) como padrão.", idPeriodo, defaultDias);
        return defaultDias;
    }

    public async Task RefreshAsync() {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoriaRepository>();
        var categorias = await repository.GetAllAsync(new FiltroCategoriaDTO { ApenasAtivos = false });
        var periodos = categorias
            .SelectMany(c => c.Periodos.Select(p =>
                new CategoriaPeriodoInfo(p.Id, p.QuantidadeDias, p.Valor, c.Id, c.Descricao)));
        AtualizarCache(periodos);
        logger.LogInformation("Cache de períodos atualizado: {Count} período(s).", _porPeriodo.Count);
    }
}
