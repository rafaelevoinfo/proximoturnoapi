using System.Text;
using Microsoft.AspNetCore.Http;
using ProximoTurnoApi.Application.Controllers;
using ProximoTurnoApi.Application.DTOs;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class SaidaSseTests {

    private static (SaidaSse saida, DefaultHttpContext contexto, MemoryStream corpo) Montar() {
        var contexto = new DefaultHttpContext();
        var corpo = new MemoryStream();
        contexto.Response.Body = corpo;
        return (new SaidaSse(contexto.Response), contexto, corpo);
    }

    [Fact]
    public async Task EscreveEventosSseComJsonEmUmaLinha() {
        var (saida, contexto, corpo) = Montar();

        await saida.IniciarAsync(new RespostaChatDTO { Tipo = TipoRespostaChat.Resposta, Jogo = new JogoChatDTO(1, "Catan") }, default);
        await saida.EscreverAsync("linha 1\nlinha 2", default);

        Assert.True(saida.Iniciado);
        Assert.Equal("text/event-stream; charset=utf-8", contexto.Response.ContentType);
        Assert.Equal("no", contexto.Response.Headers["X-Accel-Buffering"]);

        var texto = Encoding.UTF8.GetString(corpo.ToArray());
        Assert.Equal(
            "event: inicio\ndata: {\"tipo\":\"Resposta\",\"texto\":\"\",\"idConversa\":null,\"jogo\":{\"id\":1,\"nome\":\"Catan\"},\"opcoesJogo\":[]}\n\n" +
            "event: texto\ndata: {\"t\":\"linha 1\\nlinha 2\"}\n\n",
            texto);
    }

    [Fact]
    public void AntesDoPrimeiroEvento_NaoIniciou() {
        var (saida, _, _) = Montar();

        Assert.False(saida.Iniciado);
    }
}
