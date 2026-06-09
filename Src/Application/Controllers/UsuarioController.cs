
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/usuarios")]
[ApiController]
[Authorize]
public class UsuarioController(ILogger<ControllerBasico> logger, UserManager<Usuario> _userManager, IClienteRepository _clienteRepository) : ControllerBasico(logger) {

    [HttpGet("logado")]
    [Authorize]
    public async Task<IActionResult> Get() {
        _logger.LogInformation("Recuperando informações do usuário logado.");
        return await EncapsulateRequestAsync(async () => {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) {
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
}