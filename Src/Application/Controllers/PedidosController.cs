using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/pedidos")]
[ApiController]
[Authorize]
public class PedidosController(ILogger<ControllerBasico> logger,
    IPedidoRepository _pedidoRepository,
    IClienteRepository _clienteRepository,
    IJogoRepository _jogoRepository,
    ICategoriaRepository _categoriaRepository,
    UserManager<Usuario> _userManager) : ControllerBasico(logger) {


    [HttpGet()]
    public async Task<IActionResult> GetAll([FromQuery] FiltroPedidoDTO filtro) {
        return await EncapsulateRequestAsync(async () => {
            var buscarPedidosUseCase = new BuscarPedidos(_pedidoRepository, _clienteRepository, _userManager);
            var pedidos = await buscarPedidosUseCase.ExecuteAsync(User, filtro);
            if (!buscarPedidosUseCase.IsValid) {
                if (buscarPedidosUseCase.Notifications.Any(un => un.Type == UseCaseNotificationType.Forbid)) {
                    return Forbid();
                } else {
                    return StatusCode(500, buscarPedidosUseCase.AggregateErrors());
                }
            }
            return Ok(ApiResultDTO<List<PedidoDTO>>.CreateSuccessResult(pedidos, "Pedidos encontrados com sucesso"));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPedido(int id) {
        return await EncapsulateRequestAsync(async () => {
            var buscarPedidosUseCase = new BuscarPedidos(_pedidoRepository, _clienteRepository, _userManager);
            var pedido = await buscarPedidosUseCase.ExecuteAsync(User, id);
            if (!buscarPedidosUseCase.IsValid) {
                return Forbid();
            }
            if (pedido is null) {
                return NotFound(ApiResultDTO<PedidoDTO>.CreateFailureResult("Pedido não encontrado"));
            }

            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(pedido, "Pedido encontrado com sucesso"));
        });
    }

    [HttpPost]
    public async Task<IActionResult> NovoPedido(NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            var cadastroPedidoUseCase = new CadastroPedido(_pedidoRepository, _jogoRepository, _clienteRepository, _categoriaRepository, _userManager);
            var novoPedidoId = await cadastroPedidoUseCase.ExecuteAsync(User, novoPedido);
            if (novoPedidoId == 0) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(cadastroPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = novoPedidoId }, "Pedido realizado com sucesso"));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> AtualizarPedido(int id, NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            if (id != novoPedido.Id) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult("O ID do pedido na URL deve corresponder ao ID no corpo da requisição."));
            }
            var atualizarPedidoUseCase = new AtualizarPedido(_pedidoRepository, _jogoRepository, _categoriaRepository);
            await atualizarPedidoUseCase.ExecuteAsync(novoPedido);
            if (!atualizarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(atualizarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = id }, "Pedido atualizado com sucesso"));
        });
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> AtualizarStatusPedido(int id, [FromBody] StatusPedidoDTO novoStatus) {
        return await EncapsulateRequestAsync(async () => {
            var atualizarStatusPedidoUseCase = new AtualizarStatusPedido(_pedidoRepository);
            await atualizarStatusPedidoUseCase.ExecuteAsync(id, novoStatus.Status);
            if (!atualizarStatusPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(atualizarStatusPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Status do pedido atualizado com sucesso"));
        });
    }

    [HttpPut("{id}/renovar")]
    public async Task<IActionResult> RenovarPedido(int id, List<ItemPedidoRenovarDTO> itensRenovacao) {
        return await EncapsulateRequestAsync(async () => {
            var renovarPedidoUseCase = new RenovarPedido(_pedidoRepository, _jogoRepository, _categoriaRepository);
            await renovarPedidoUseCase.ExecuteAsync(id, itensRenovacao);
            if (!renovarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(renovarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido renovado com sucesso"));
        });
    }

    [HttpPut("{id}/devolver")]
    public async Task<IActionResult> DevolverItemsPedido(int id, [FromBody] List<int>? idsItensDevolvidos) {
        return await EncapsulateRequestAsync(async () => {
            var devolverPedidoUseCase = new DevolverItensPedido(_pedidoRepository);
            await devolverPedidoUseCase.ExecuteAsync(id, idsItensDevolvidos);
            if (!devolverPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(devolverPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Items devolvidos com sucesso"));
        });
    }



    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelarPedido(int id) {
        return await EncapsulateRequestAsync(async () => {
            var atualizarStatusPedidoUseCase = new AtualizarStatusPedido(_pedidoRepository);
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
