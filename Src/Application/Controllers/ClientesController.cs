using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/clientes")]
[ApiController]
public class ClientesController(ILogger<ControllerBasico> logger, IClienteRepository _repository) : ControllerBasico(logger) {

    [HttpGet]
    public async Task<IActionResult> GetClientes(FiltroClienteDTO filtro) {
        _logger.LogInformation("Recuperando clientes.");
        return await EncapsulateRequestAsync(async () => {
            var clientes = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<ClienteDTO>>.CreateSuccessResult([.. clientes.Select(ClienteDTO.FromModel)], "Clientes recuperados com sucesso."));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCliente(int id) {
        return await EncapsulateRequestAsync(async () => {
            var cliente = await _repository.GetByIdAsync(id);
            if (cliente == null) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult($"Cliente de id {id} não encontrado."));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(ClienteDTO.FromModel(cliente), "Cliente recuperado com sucesso."));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCliente(int id, ClienteDTO cliente) {
        return await EncapsulateRequestAsync(async () => {
            var atualizarCliente = new AtualizarCliente(_repository);
            var result = await atualizarCliente.ExecuteAsync(cliente);
            if (!result) {
                return BadRequest(ApiResultDTO<ClienteDTO>.CreateFailureResult(atualizarCliente.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente atualizado com sucesso."));
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostCliente(ClienteDTO clienteDto) {
        return await EncapsulateRequestAsync(async () => {
            var cadastroClienteUseCase = new CadastroCliente(_repository);
            var result = await cadastroClienteUseCase.ExecuteAsync(clienteDto);
            if (!result) {
                return BadRequest(ApiResultDTO<ClienteDTO>.CreateFailureResult(cadastroClienteUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente criado com sucesso."));
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCliente(int id) {
        return await EncapsulateRequestAsync(async () => {
            if (!await _repository.DeleteAsync(id)) {
                return NotFound(ApiResultDTO<ClienteDTO>.CreateFailureResult($"Cliente de id {id} não encontrado."));
            }
            return Ok(ApiResultDTO<ClienteDTO>.CreateSuccessResult(null, "Cliente excluído com sucesso."));
        });
    }


}
