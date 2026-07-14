using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class CategoriaPeriodoCacheTests {
    private static CategoriaPeriodoCache CriarCache() =>
        new(scopeFactory: null!, logger: NullLogger<CategoriaPeriodoCache>.Instance);

    [Fact]
    public void GetQuantidadeDias_QuandoPeriodoExiste_RetornaValorDoCache() {
        var cache = CriarCache();
        cache.AtualizarCache([new CategoriaPeriodoInfo(5, 7, 30m, 1, "Standard")]);

        Assert.Equal(7, cache.GetQuantidadeDias(5));
    }

    [Fact]
    public void GetQuantidadeDias_QuandoPeriodoAusente_RetornaDefault() {
        var cache = CriarCache();
        cache.AtualizarCache([]);

        Assert.Equal(1, cache.GetQuantidadeDias(999));
    }

    [Fact]
    public void TryGetPeriodo_QuandoExiste_RetornaTrueEInfo() {
        var cache = CriarCache();
        cache.AtualizarCache([new CategoriaPeriodoInfo(5, 7, 30m, 2, "Premium")]);

        Assert.True(cache.TryGetPeriodo(5, out var info));
        Assert.Equal(2, info!.IdCategoria);
    }
}
