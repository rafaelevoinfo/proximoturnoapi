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

    [HttpPost("mensagens")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> EnviarMensagem([FromBody] PerguntaChatDTO pergunta, CancellationToken cancellationToken) {
        return await EncapsulateRequestAsync(async () => {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario is null) {
                return Unauthorized();
            }

            var usuarioChat = new UsuarioChat(usuario.Id, usuario.Email, User.IsInRole(Roles.Admin));
            var resposta = await _responderPergunta.ExecuteAsync(usuarioChat, pergunta, cancellationToken);
            if (!_responderPergunta.IsValid) {
                return BadRequest(ApiResultDTO<RespostaChatDTO>.CreateFailureResult(_responderPergunta.AggregateErrors()));
            }

            return Ok(ApiResultDTO<RespostaChatDTO>.CreateSuccessResult(resposta));
        });
    }

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
