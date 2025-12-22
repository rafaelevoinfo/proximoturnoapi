using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;

namespace ProximoTurnoApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PedidosAlugueisController : ControllerBase {
    private readonly IPedidoAluguelRepository _repository;

    public PedidosAlugueisController(IPedidoAluguelRepository repository) {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Pedido>>> GetPedidosAlugueis() {
        return await _repository.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Pedido>> GetPedidoAluguel(int id) {
        var pedidoAluguel = await _repository.GetByIdAsync(id);

        if (pedidoAluguel == null) {
            return NotFound();
        }

        return pedidoAluguel;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutPedidoAluguel(int id, Pedido pedidoAluguel) {
        if (id != pedidoAluguel.Id) {
            return BadRequest();
        }

        await _repository.UpdateAsync(pedidoAluguel);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Pedido>> PostPedidoAluguel(Pedido pedidoAluguel) {
        await _repository.AddAsync(pedidoAluguel);

        return CreatedAtAction("GetPedidoAluguel", new { id = pedidoAluguel.Id }, pedidoAluguel);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePedidoAluguel(int id) {
        var pedidoAluguel = await _repository.GetByIdAsync(id);
        if (pedidoAluguel == null) {
            return NotFound();
        }

        await _repository.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("{pedidoId}/devolver")]
    public async Task<IActionResult> DevolverJogosPedido(int pedidoId) {
        var pedido = await _repository.GetByIdAsync(pedidoId);
        if (pedido == null) {
            return NotFound();
        }

        await _repository.DevolverJogosPedidoAsync(pedidoId);

        return NoContent();
    }

    [HttpPost("{pedidoId}/devolver/{jogoId}")]
    public async Task<IActionResult> DevolverJogoAvulso(int pedidoId, int jogoId) {
        var pedido = await _repository.GetByIdAsync(pedidoId);
        if (pedido == null || pedido.Jogos == null) {
            return NotFound();
        }

        var jogo = pedido.Jogos.FirstOrDefault(j => j.IdJogo == jogoId);
        if (jogo == null) {
            return NotFound();
        }

        await _repository.DevolverJogoAvulsoAsync(pedidoId, jogoId);

        return NoContent();
    }

    // [HttpPost("renovar")]
    // public async Task<ActionResult<Pedido>> RenovarPedido(RenovarPedidoRequest request) {
    //     var novoPedido = await _repository.RenovarPedidoAsync(request);
    //     if (novoPedido == null) {
    //         return NotFound();
    //     }

    //     return CreatedAtAction("GetPedidoAluguel", new { id = novoPedido.Id }, novoPedido);
    // }
}
