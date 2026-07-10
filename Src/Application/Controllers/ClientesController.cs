using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/clientes")]
[ApiController]
public class ClientesController(ILogger<ControllerBasico> logger,
                            IClienteRepository _repository,
                            CadastroCliente _cadastroClienteUseCase,
                            AtualizarCliente _atualizarClienteUseCase,
                            AtualizarStatusCliente _atualizarStatusClienteUseCase,
                            EnviarEmailsClientes _enviarEmailsClientes,
                            UserManager<Usuario> _userManager) : ControllerBasico(logger) {

    /// <summary>
    /// Um usuário consegue logar quando já definiu uma senha e não está bloqueado por lockout.
    /// Clientes importados possuem usuário sem senha, então aparecem como login inativo.
    /// </summary>
    private static bool PodeLogar(Usuario? usuario) =>
        usuario is not null
        && usuario.PasswordHash is not null
        && !(usuario.LockoutEnabled && usuario.LockoutEnd > DateTimeOffset.UtcNow);

    /// <summary>Resolve o login de vários clientes de uma vez, evitando uma consulta por linha do grid.</summary>
    private async Task<Dictionary<string, bool>> ObterLoginAtivoPorEmailAsync(IEnumerable<string> emails) {
        var normalizados = emails.Select(e => e.ToUpperInvariant()).ToHashSet();

        // O filtro é aplicado em memória porque o provider MySQL não traduz um Contains sobre
        // coleção ("Expression '@normalizados' ... does not have a type mapping assigned").
        // Só as quatro colunas necessárias são trazidas, e a tabela tem a ordem de grandeza da
        // própria lista de clientes que o grid já carrega.
        var usuarios = await _userManager.Users
            .Select(u => new { u.NormalizedEmail, u.PasswordHash, u.LockoutEnabled, u.LockoutEnd })
            .ToListAsync();

        var agora = DateTimeOffset.UtcNow;
        return usuarios
            .Where(u => u.NormalizedEmail is not null && normalizados.Contains(u.NormalizedEmail))
            .ToDictionary(
                u => u.NormalizedEmail!,
                u => u.PasswordHash is not null && !(u.LockoutEnabled && u.LockoutEnd > agora));
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetClientes(FiltroClienteDTO filtro) {
        _logger.LogInformation("Recuperando clientes.");
        return await EncapsulateRequestAsync(async () => {
            var clientes = await _repository.GetAllAsync(filtro);
            var loginAtivo = await ObterLoginAtivoPorEmailAsync(clientes.Select(c => c.Email));

            var dtos = clientes.Select(c => {
                var dto = ClienteDTO.FromModel(c);
                dto.LoginAtivo = loginAtivo.GetValueOrDefault(c.Email.ToUpperInvariant());
                return dto;
            }).ToList();

            return Ok(ApiResultDTO<List<ClienteDTO>>.CreateSuccessResult(dtos, "Clientes recuperados com sucesso."));
        });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetCliente([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var cliente = await _repository.GetByIdAsync(id);
            if (cliente == null) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult($"Cliente de id {id} não encontrado."));
            }
            var dto = ClienteDTO.FromModel(cliente);
            dto.LoginAtivo = PodeLogar(await _userManager.FindByEmailAsync(cliente.Email));

            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(dto, "Cliente recuperado com sucesso."));
        });
    }

    [HttpGet("{id:int}/perfil")]
    [Authorize]
    public async Task<IActionResult> GetPerfilCliente([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) {
                _logger.LogWarning("Usuário logado não encontrado.");
                return NotFound();
            }
            var idCliente = await _repository.GetIdByEmailAsync(user.Email ?? "");
            if (idCliente != id && !await _userManager.IsInRoleAsync(user, Roles.Admin)) {
                _logger.LogWarning("Usuário logado tentou acessar o perfil de outro cliente.");
                return Forbid();
            }
            var cliente = await _repository.GetByIdAsync(id);
            if (cliente == null) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult($"Cliente de id {id} não encontrado."));
            }
            //Futuramente podemos ter um outro DTO para diferenciar o perfil do cliente usado por um admin e o perfil do cliente usado pelo próprio cliente
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(ClienteDTO.FromModel(cliente), "Cliente recuperado com sucesso."));
        });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> PutCliente([FromRoute] int id, [FromBody] ClienteDTO cliente) {
        return await EncapsulateRequestAsync(async () => {
            if (id != cliente.Id) {
                return BadRequest(ApiResultDTO<ClienteDTO>.CreateFailureResult("ID do cliente na URL não corresponde ao ID no corpo da requisição."));
            }
            var result = await _atualizarClienteUseCase.ExecuteAsync(cliente);
            if (!result) {
                return BadRequest(ApiResultDTO<ClienteDTO>.CreateFailureResult(_atualizarClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente atualizado com sucesso."));
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostCliente([FromBody] ClienteDTO clienteDto, [FromQuery] bool enviarEmail = true) {
        return await EncapsulateRequestAsync(async () => {
            // Endpoint anônimo: apenas um Admin pode suprimir o e-mail de ativação, caso contrário
            // seria possível criar contas em nome de terceiros sem que o dono do e-mail seja avisado.
            var enviarEmailAtivacao = enviarEmail || !User.IsInRole(Roles.Admin);

            var idCliente = await _cadastroClienteUseCase.ExecuteAsync(clienteDto, enviarEmailAtivacao);
            if (idCliente == 0) {
                return BadRequest(ApiResultDTO<ClienteDTO>.CreateFailureResult(_cadastroClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(new ClienteDTO() { Id = idCliente }, "Cliente criado com sucesso."));
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteCliente([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var result = await _atualizarStatusClienteUseCase.ExecuteAsync(id, false, User);
            if (!result) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult(_atualizarStatusClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente inativado com sucesso."));
        });
    }

    [HttpPost("{id:int}/ativar")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AtivarCliente([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var result = await _atualizarStatusClienteUseCase.ExecuteAsync(id, true, User);
            if (!result) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult(_atualizarStatusClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente ativado com sucesso."));
        });
    }

    [HttpPost("enviar-email")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> EnviarEmail([FromBody] EnviarEmailsClientesRequest dto) =>
        await EncapsulateRequestAsync(async () => {
            var result = await _enviarEmailsClientes.ExecuteAsync(dto);
            if (!_enviarEmailsClientes.IsValid) {
                var notification = _enviarEmailsClientes.Notifications.FirstOrDefault();
                if (notification?.Type == UseCaseNotificationType.NotFound)
                    return NotFound(ApiResultDTO<object>.CreateFailureResult(_enviarEmailsClientes.AggregateErrors()));

                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_enviarEmailsClientes.AggregateErrors()));
            }

            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Emails enviados com sucesso."));
        });
}
