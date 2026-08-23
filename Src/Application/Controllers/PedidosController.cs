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
public class PedidosController(ILogger<PedidosController> logger,
    BuscarPedidos _buscarPedidosUseCase,
    CadastroPedido _cadastroPedidoUseCase,
    AtualizarPedido _atualizarPedidoUseCase,
    EntregarPedido _entregarPedidoUseCase,
    CancelarPedido _cancelarPedidoUseCase,
    RenovarPedido _renovarPedidoUseCase,
    DevolverItensPedido _devolverPedidoUseCase) : ControllerBasico(logger) {


    [HttpGet()]
    public async Task<IActionResult> GetAll([FromQuery] FiltroPedidoDTO filtro) {
        return await EncapsulateRequestAsync(async () => {
            var pedidos = await _buscarPedidosUseCase.ExecuteAsync(User, filtro);
            if (!_buscarPedidosUseCase.IsValid) {
                if (_buscarPedidosUseCase.Notifications.Any(un => un.Type == UseCaseNotificationType.Forbid)) {
                    return Forbid();
                } else {
                    return StatusCode(500, _buscarPedidosUseCase.AggregateErrors());
                }
            }
            return Ok(ApiResultDTO<List<PedidoDTO>>.CreateSuccessResult(pedidos, "Pedidos encontrados com sucesso"));
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPedido([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var pedido = await _buscarPedidosUseCase.ExecuteAsync(User, id);
            if (!_buscarPedidosUseCase.IsValid) {
                return Forbid();
            }
            if (pedido is null) {
                return NotFound(ApiResultDTO<PedidoDTO>.CreateFailureResult("Pedido não encontrado"));
            }

            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(pedido, "Pedido encontrado com sucesso"));
        });
    }

    [HttpPost]
    public async Task<IActionResult> NovoPedido([FromBody] NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            var novoPedidoId = await _cadastroPedidoUseCase.ExecuteAsync(User, novoPedido);
            if (novoPedidoId == 0) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_cadastroPedidoUseCase.AggregateErrors()));
            }

            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = novoPedidoId }, "Pedido realizado com sucesso"));
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> AtualizarPedido([FromRoute] int id, [FromBody] NovoPedidoDTO novoPedido) {
        return await EncapsulateRequestAsync(async () => {
            if (id != novoPedido.Id) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult("O ID do pedido na URL deve corresponder ao ID no corpo da requisição."));
            }
            await _atualizarPedidoUseCase.ExecuteAsync(novoPedido);
            if (!_atualizarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_atualizarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(new PedidoDTO() { Id = id }, "Pedido atualizado com sucesso"));
        });
    }

    [HttpPut("{id:int}/entregar")]
    public async Task<IActionResult> EntregarPedido([FromRoute] int id, [FromBody] EntregarPedidoDTO? dto) {
        return await EncapsulateRequestAsync(async () => {
            await _entregarPedidoUseCase.ExecuteAsync(id, dto?.DataDevolucao);
            if (!_entregarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_entregarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido entregue com sucesso"));
        });
    }

    [HttpPut("{id:int}/renovar")]
    public async Task<IActionResult> RenovarPedido([FromRoute] int id, [FromBody] List<ItemPedidoRenovarDTO> itensRenovacao) {
        return await EncapsulateRequestAsync(async () => {
            await _renovarPedidoUseCase.ExecuteAsync(id, itensRenovacao);
            if (!_renovarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_renovarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido renovado com sucesso"));
        });
    }

    [HttpPut("{id:int}/devolver")]
    public async Task<IActionResult> DevolverItemsPedido([FromRoute] int id, [FromBody] List<int>? idsItensDevolvidos) {
        return await EncapsulateRequestAsync(async () => {
            await _devolverPedidoUseCase.ExecuteAsync(id, idsItensDevolvidos);
            if (!_devolverPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_devolverPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Items devolvidos com sucesso"));
        });
    }



    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelarPedido([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            await _cancelarPedidoUseCase.ExecuteAsync(id);
            if (!_cancelarPedidoUseCase.IsValid) {
                return BadRequest(ApiResultDTO<PedidoDTO>.CreateFailureResult(_cancelarPedidoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<PedidoDTO>.CreateSuccessResult(null, "Pedido cancelado com sucesso"));
        });
    }


}
