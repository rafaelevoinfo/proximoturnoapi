using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/chat")]
[ApiController]
[Authorize]
public class ChatController(ILogger<ChatController> logger,
                            UserManager<Usuario> _userManager,
                            IClienteRepository _clienteRepository,
                            ResponderPerguntaRegras _responderPergunta,
                            ObterSaldoChat _obterSaldo) : ControllerBasico(logger) {

    public const string PoliticaLimite = "chat";

    /// <summary>
    /// Pergunta ao assistente. Erro de validação ou de login volta como JSON, com o status
    /// HTTP de sempre. Daí em diante a resposta é SSE: um evento <c>resposta</c> quando não
    /// passa pelo modelo, ou <c>inicio</c>, vários <c>texto</c> e <c>fim</c> quando passa.
    /// </summary>
    [HttpPost("mensagens")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task EnviarMensagem([FromBody] PerguntaChatDTO pergunta, CancellationToken cancellationToken) {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var usuarioChat = new UsuarioChat(usuario.Id, usuario.Email, User.IsInRole(Roles.Admin));
        var saida = new SaidaSse(Response);

        RespostaChatDTO? resposta;
        try {
            resposta = await _responderPergunta.ExecuteAsync(usuarioChat, pergunta, saida, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // O usuario fechou a tela ou a conexao caiu: nao ha para quem responder.
            return;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro no chat de regras");
            if (saida.Iniciado) {
                await saida.EventoAsync("erro", new { mensagem = MensagemErro }, CancellationToken.None);
            } else {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Response.WriteAsJsonAsync(ApiResultDTO<string>.CreateFailureResult(MensagemErro), CancellationToken.None);
            }
            return;
        }

        if (!_responderPergunta.IsValid) {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(
                ApiResultDTO<RespostaChatDTO>.CreateFailureResult(_responderPergunta.AggregateErrors()), cancellationToken);
            return;
        }

        await saida.EventoAsync(saida.Iniciado ? "fim" : "resposta", resposta!, cancellationToken);
    }

    private const string MensagemErro = "Não consegui falar com o assistente agora. Tente de novo em instantes.";

    /// <summary>Crédito do chat de um cliente. Só admin: o cliente não vê o próprio saldo.</summary>
    [HttpGet("creditos/cliente/{idCliente:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ObterCreditosCliente([FromRoute] int idCliente, CancellationToken cancellationToken) {
        return await EncapsulateRequestAsync(async () => {
            var cliente = await _clienteRepository.GetByIdAsync(idCliente);
            if (cliente is null) {
                return NotFound(ApiResultDTO<SaldoChatDTO>.CreateFailureResult("Cliente não encontrado."));
            }

            // Cliente sem login ainda nao gastou nada no chat: so os alugueis e o bonus contam.
            var usuario = await _userManager.FindByEmailAsync(cliente.Email);
            var admin = usuario is not null && await _userManager.IsInRoleAsync(usuario, Roles.Admin);

            var saldo = await _obterSaldo.ExecuteAsync(usuario?.Id ?? "", cliente.Email, admin, cancellationToken);
            return Ok(ApiResultDTO<SaldoChatDTO>.CreateSuccessResult(saldo));
        });
    }
}
