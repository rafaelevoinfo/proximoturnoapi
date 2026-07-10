using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetClientes(FiltroClienteDTO filtro) {
        _logger.LogInformation("Recuperando clientes.");
        return await EncapsulateRequestAsync(async () => {
            var clientes = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<ClienteDTO>>.CreateSuccessResult([.. clientes.Select(ClienteDTO.FromModel)], "Clientes recuperados com sucesso."));
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
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(ClienteDTO.FromModel(cliente), "Cliente recuperado com sucesso."));
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
