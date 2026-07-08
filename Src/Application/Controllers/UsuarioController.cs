
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/usuarios")]
[ApiController]
public class UsuarioController(ILogger<ControllerBasico> logger, UserManager<Usuario> _userManager, IClienteRepository _clienteRepository, IConfiguration _configuration, IResetSenhaLinkService _resetSenhaLinkService) : ControllerBasico(logger) {

    [HttpGet("logado")]
    [Authorize]
    public async Task<IActionResult> Get() {
        _logger.LogInformation("Recuperando informações do usuário logado.");
        return await EncapsulateRequestAsync(async () => {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) {
                _logger.LogWarning("Usuário logado não encontrado.");
                return NotFound();
            }

            var idCliente = await _clienteRepository.GetIdByEmailAsync(user.Email ?? "");

            var usuarioDto = new UsuarioDTO() {
                Id = user.Id,
                Nome = user.Nome ?? user.Email ?? "",
                Email = user.Email ?? "",
                IsAdmin = await _userManager.IsInRoleAsync(user, Roles.Admin),
                IdCliente = idCliente
            };
            return Ok(ApiResultDTO<UsuarioDTO>.CreateSuccessResult(usuarioDto));
        });
    }

    [HttpGet("resetarSenha")]
    public async Task<IActionResult> ResetPassword(string email, [FromServices] IEmailSender<Usuario> emailService) {
        _logger.LogInformation("Iniciando processo de reset de senha.");
        return await EncapsulateRequestAsync(async () => {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) {
                _logger.LogWarning("Foi feita uma tentativa de reset de senha para um email não cadastrado: {Email}", email);
                return NotFound();
            }

            var resetLink = await _resetSenhaLinkService.GerarLinkAsync(email);
            if (resetLink == null) {
                return NotFound();
            }

            await emailService.SendPasswordResetLinkAsync(user, email, resetLink);
            return Ok();
        });
    }
}