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
                            AtualizarStatusCliente _atualizarStatusClienteUseCase) : ControllerBasico(logger) {

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
    public async Task<IActionResult> PostCliente([FromBody] ClienteDTO clienteDto) {
        return await EncapsulateRequestAsync(async () => {
            var idCliente = await _cadastroClienteUseCase.ExecuteAsync(clienteDto);
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
            var result = await _atualizarStatusClienteUseCase.ExecuteAsync(id, false);
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
            var result = await _atualizarStatusClienteUseCase.ExecuteAsync(id, true);
            if (!result) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult(_atualizarStatusClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente ativado com sucesso."));
        });
    }


}
