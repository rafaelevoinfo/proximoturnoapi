using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class ReservasCarrinhoMemoriaTests {
    private sealed class RelogioFixo : TimeProvider {
        public DateTimeOffset Agora { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
        public void Avancar(TimeSpan tempo) => Agora += tempo;
    }

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [Fact]
    public void Reservar_UltimaCopia_BloqueiaOutroCarrinhoMasNaoODono() {
        var reservas = new ReservasCarrinhoMemoria(new RelogioFixo());

        Assert.NotNull(reservas.Reservar(A, idJogo: 1, copiasDisponiveis: 1));

        Assert.Null(reservas.Reservar(B, 1, 1));
        Assert.Equal(1, reservas.SeguradasPorOutros(1, B));
        Assert.Equal(0, reservas.SeguradasPorOutros(1, A));
        Assert.Equal(1, reservas.SeguradasPorOutros(1, null));
    }

    [Fact]
    public void Reservar_DuasCopias_DoisCarrinhosReservamETerceiroNao() {
        var reservas = new ReservasCarrinhoMemoria(new RelogioFixo());

        Assert.NotNull(reservas.Reservar(A, 1, 2));
        Assert.NotNull(reservas.Reservar(B, 1, 2));
        Assert.Null(reservas.Reservar(Guid.NewGuid(), 1, 2));
    }

    [Fact]
    public void Reserva_VenceEm15Minutos() {
        var relogio = new RelogioFixo();
        var reservas = new ReservasCarrinhoMemoria(relogio);
        var fim = reservas.Reservar(A, 1, 1);

        Assert.Equal(relogio.Agora.AddMinutes(15), fim);

        relogio.Avancar(TimeSpan.FromMinutes(14));
        Assert.Equal(1, reservas.SeguradasPorOutros(1, B));

        relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(0, reservas.SeguradasPorOutros(1, B));
        Assert.Empty(reservas.DoCarrinho(A));
        Assert.NotNull(reservas.Reservar(B, 1, 1));
    }

    [Fact]
    public void Reservar_DeNovo_NaoRenovaOPrazo() {
        var relogio = new RelogioFixo();
        var reservas = new ReservasCarrinhoMemoria(relogio);
        var fim = reservas.Reservar(A, 1, 1);

        relogio.Avancar(TimeSpan.FromMinutes(10));

        Assert.Equal(fim, reservas.Reservar(A, 1, 1));
    }

    [Fact]
    public void Liberar_DevolveACopia() {
        var reservas = new ReservasCarrinhoMemoria(new RelogioFixo());
        reservas.Reservar(A, 1, 1);
        reservas.Reservar(A, 2, 1);

        reservas.Liberar(A, 1);
        Assert.Equal(0, reservas.SeguradasPorOutros(1, B));
        Assert.Equal(1, reservas.SeguradasPorOutros(2, B));

        reservas.LiberarTudo(A);
        Assert.Equal(0, reservas.SeguradasPorOutros(2, B));
    }

    [Theory]
    [InlineData(StatusJogo.Disponivel, 1, 1, StatusJogo.Reservado)]
    [InlineData(StatusJogo.Disponivel, 2, 1, StatusJogo.Disponivel)]
    [InlineData(StatusJogo.Disponivel, 1, 0, StatusJogo.Disponivel)]
    [InlineData(StatusJogo.Alugado, 0, 0, StatusJogo.Alugado)]
    public void ComReservasDeCarrinho(StatusJogo status, int disponiveis, int seguradas, StatusJogo esperado) {
        Assert.Equal(esperado, StatusDoJogo.ComReservasDeCarrinho(status, disponiveis, seguradas));
    }
}
