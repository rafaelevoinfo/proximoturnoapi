using System.Linq;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Tests.Domain;

public class JogoCardDTOTests {
    private static Jogo JogoComCopias(params StatusJogo[] status) => new() {
        Id = 1,
        Nome = "Jogo",
        Descricao = "x",
        IdCategoria = 1,
        Copias = status.Select((s, i) => new JogoCopia { Id = i + 1, IdJogo = 1, Status = s }).ToList()
    };

    [Fact]
    public void Status_QuandoTodasCopiasDesativadas_RetornaDesativado() {
        var dto = JogoCardDTO.FromModel(JogoComCopias(StatusJogo.Desativado, StatusJogo.Desativado));
        Assert.Equal(StatusJogo.Desativado, dto.Status);
    }

    [Fact]
    public void Status_QuandoTemCopiaDisponivel_RetornaDisponivel() {
        var dto = JogoCardDTO.FromModel(JogoComCopias(StatusJogo.Desativado, StatusJogo.Disponivel));
        Assert.Equal(StatusJogo.Disponivel, dto.Status);
    }

    [Fact]
    public void Status_QuandoMixSemDisponivel_IgnoraDesativadasEUsaMenorAtivo() {
        var dto = JogoCardDTO.FromModel(JogoComCopias(StatusJogo.Desativado, StatusJogo.Alugado));
        Assert.Equal(StatusJogo.Alugado, dto.Status);
    }
}
