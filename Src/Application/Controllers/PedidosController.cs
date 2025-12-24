using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/pedidos")]
[ApiController]
public class PedidosController(ILogger<ControllerBasico> logger,
    IPedidoRepository _pedidoRepository,
    IClienteRepository _clienteRepository,
    IJogoRepository _jogoRepository) : ControllerBasico(logger) {


    [HttpGet()]
    public async Task<IActionResult> GetAll(FiltroPedidoDTO filtro) {
        return await EncapsulateRequestAsync(async () => {
            var pedidos = (await _pedidoRepository.GetAllAsync(filtro))
                .Select(PedidoDTO.FromModel)
                .ToList();
            return Ok(ApiResultDTO<List<PedidoDTO>>.CreateSuccessResult(pedidos, "Pedidos encontrados com sucesso"));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPedido(int id) {
        return await EncapsulateRequestAsync(async () => {
            var pedido = await _pedidoRepository.GetByIdAsync(id);
            if (pedido == null) {
                return NotFound(ApiResultDTO<PedidoDTO>.CreateFailureResult("Pedido não encontrado"));
            }

            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(PedidoDTO.FromModel(pedido), "Pedido encontrado com sucesso"));
        });
    }

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

    [HttpPut("{id}/status")]
    public async Task<IActionResult> AtualizarStatusPedido(int id, StatusPedido novoStatus) {
        return await EncapsulateRequestAsync(async () => {
            var atualizarStatusPedidoUseCase = new AtualizarStatusPedido(_pedidoRepository, _jogoRepository, _clienteRepository);
            await atualizarStatusPedidoUseCase.ExecuteAsync(id, novoStatus);
            if (!atualizarStatusPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(atualizarStatusPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Status do pedido atualizado com sucesso"));
        });
    }

    [HttpPut("{id}/renovar")]
    public async Task<IActionResult> RenovarPedido(int id, List<ItemPedidoDTO> itensRenovacao) {
        return await EncapsulateRequestAsync(async () => {
            var renovarPedidoUseCase = new RenovarPedido(_pedidoRepository, _jogoRepository, _clienteRepository);
            await renovarPedidoUseCase.ExecuteAsync(id, itensRenovacao);
            if (!renovarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(renovarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido renovado com sucesso"));
        });
    }

    [HttpPut("{id}/devolver")]
    public async Task<IActionResult> DevolverItemsPedido(int id, List<int> idsItensDevolvidos) {
        return await EncapsulateRequestAsync(async () => {
            var renovarPedidoUseCase = new DevolverItensPedido(_jogoRepository, _pedidoRepository);
            await renovarPedidoUseCase.ExecuteAsync(id, idsItensDevolvidos);
            if (!renovarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(renovarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Items devolvidos com sucesso"));
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelarPedido(int id) {
        return await EncapsulateRequestAsync(async () => {
            var atualizarStatusPedidoUseCase = new AtualizarStatusPedido(_pedidoRepository, _jogoRepository, _clienteRepository);
            await atualizarStatusPedidoUseCase.ExecuteAsync(id, StatusPedido.Cancelado);
            if (!atualizarStatusPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(atualizarStatusPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido cancelado com sucesso"));
        });
    }

    // [HttpPost("{pedidoId}/devolver")]
    // public async Task<IActionResult> DevolverJogosPedido(int pedidoId) {
    //     var pedido = await _repository.GetByIdAsync(pedidoId);
    //     if (pedido == null) {
    //         return NotFound();
    //     }

    //     await _repository.DevolverJogosPedidoAsync(pedidoId);

    //     return NoContent();
    // }


}
