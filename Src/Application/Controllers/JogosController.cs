using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/jogos")]
[ApiController]
public class JogosController : ControllerBase {
    private readonly IJogoRepository _repository;

    public JogosController(IJogoRepository repository) {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Jogo>>> GetJogos() {
        return await _repository.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Jogo>> GetJogo(int id) {
        var jogo = await _repository.GetByIdAsync(id);

        if (jogo == null) {
            return NotFound();
        }

        return jogo;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutJogo(int id, Jogo jogo) {
        if (id != jogo.Id) {
            return BadRequest();
        }

        await _repository.UpdateAsync(jogo);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Jogo>> PostJogo(Jogo jogo) {
        await _repository.AddAsync(jogo);

        return CreatedAtAction("GetJogo", new { id = jogo.Id }, jogo);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJogo(int id) {
        var jogo = await _repository.GetByIdAsync(id);
        if (jogo == null) {
            return NotFound();
        }

        await _repository.DeleteAsync(id);

        return NoContent();
    }
}
