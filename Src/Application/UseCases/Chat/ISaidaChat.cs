using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Para onde vai a resposta do modelo enquanto é gerada. O caso de uso não sabe de HTTP: quem
/// implementa (o controller, com SSE) decide como entregar cada pedaço ao navegador.
/// </summary>
public interface ISaidaChat {

    /// <summary>
    /// Chamado uma vez, antes do primeiro pedaço de texto: a conversa e o jogo da resposta que
    /// vai começar. Respostas que não passam pelo modelo (sem saldo, confirmar jogo etc.) nunca
    /// chegam aqui; voltam inteiras no retorno de <c>ExecuteAsync</c>.
    /// </summary>
    Task IniciarAsync(RespostaChatDTO cabecalho, CancellationToken cancellationToken);

    Task EscreverAsync(string trecho, CancellationToken cancellationToken);

    /// <summary>
    /// O modelo pediu uma ferramenta (buscar no manual, listar jogos). A tela volta a mostrar
    /// que está consultando, mesmo que algum texto já tenha saído antes.
    /// </summary>
    Task ConsultandoAsync(CancellationToken cancellationToken);
}
