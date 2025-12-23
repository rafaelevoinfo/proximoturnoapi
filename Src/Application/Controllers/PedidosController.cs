using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/pedidos")]
[ApiController]
public class PedidosController(ILogger<ControllerBasico> logger,
    IPedidoRepository _pedidoRepository,
    IClienteRepository _clienteRepository,
    IJogoRepository _jogoRepository) : ControllerBasico(logger) {

    // [HttpGet]
    // public async Task<ActionResult<IEnumerable<PedidoDTO>>> GetPedidosAlugueis() {
    //     //return await _repository.GetAllAsync();
    // }

    // [HttpGet("{id}")]
    // public async Task<IActionResult> GetPedido(int id) {
    //     return await EncapsulateRequestAsync(async () => {
    //         var pedido = await _repository.GetByIdAsync(id);
    //         if (pedido == null) {
    //             return NotFound(ApiResultDTO<PedidoDTO>.CreateFailure("Pedido não encontrado"));
    //         }

    //         return pedido;
    //     });
    // }

    [HttpPut("{id}")]
    public async Task<IActionResult> AtualizarPedido(int id, NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            if (id != novoPedido.Id) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult("O ID do pedido na URL deve corresponder ao ID no corpo da requisição."));
            }
            var atualizarPedidoUseCase = new AtualizarPedido(_pedidoRepository, _clienteRepository, _jogoRepository);
            await atualizarPedidoUseCase.ExecuteAsync(novoPedido);
            if (!atualizarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(atualizarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = id }, "Pedido atualizado com sucesso"));
        });
    }

    [HttpPost]
    public async Task<IActionResult> NovoPedido(NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            var cadastroPedidoUseCase = new CadastroPedido(_pedidoRepository, _clienteRepository, _jogoRepository);
            var novoPedidoId = await cadastroPedidoUseCase.ExecuteAsync(novoPedido);
            if (novoPedidoId == 0) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(cadastroPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = novoPedidoId }, "Pedido realizado com sucesso"));
        });
    }

    // [HttpDelete("{id}")]
    // public async Task<IActionResult> DeletePedidoAluguel(int id) {
    //     var pedidoAluguel = await _repository.GetByIdAsync(id);
    //     if (pedidoAluguel == null) {
    //         return NotFound();
    //     }

    //     await _repository.DeleteAsync(id);

    //     return NoContent();
    // }

    // [HttpPost("{pedidoId}/devolver")]
    // public async Task<IActionResult> DevolverJogosPedido(int pedidoId) {
    //     var pedido = await _repository.GetByIdAsync(pedidoId);
    //     if (pedido == null) {
    //         return NotFound();
    //     }

    //     await _repository.DevolverJogosPedidoAsync(pedidoId);

    //     return NoContent();
    // }

    // [HttpPost("{pedidoId}/devolver/{jogoId}")]
    // public async Task<IActionResult> DevolverJogoAvulso(int pedidoId, int jogoId) {
    //     var pedido = await _repository.GetByIdAsync(pedidoId);
    //     if (pedido == null || pedido.Jogos == null) {
    //         return NotFound();
    //     }

    //     var jogo = pedido.Jogos.FirstOrDefault(j => j.IdJogo == jogoId);
    //     if (jogo == null) {
    //         return NotFound();
    //     }

    //     await _repository.DevolverJogoAvulsoAsync(pedidoId, jogoId);

    //     return NoContent();
    // }

    // [HttpPost("renovar")]
    // public async Task<ActionResult<Pedido>> RenovarPedido(RenovarPedidoRequest request) {
    //     var novoPedido = await _repository.RenovarPedidoAsync(request);
    //     if (novoPedido == null) {
    //         return NotFound();
    //     }

    //     return CreatedAtAction("GetPedidoAluguel", new { id = novoPedido.Id }, novoPedido);
    // }
}
