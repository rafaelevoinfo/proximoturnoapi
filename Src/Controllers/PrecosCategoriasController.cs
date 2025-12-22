using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;

namespace ProximoTurnoApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PrecosCategoriasController : ControllerBase {
    private readonly IPrecoCategoriaRepository _repository;

    public PrecosCategoriasController(IPrecoCategoriaRepository repository) {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoriaPreco>>> GetPrecosCategorias() {
        return await _repository.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoriaPreco>> GetPrecoCategoria(int id) {
        var precoCategoria = await _repository.GetByIdAsync(id);

        if (precoCategoria == null) {
            return NotFound();
        }

        return precoCategoria;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutPrecoCategoria(int id, CategoriaPreco precoCategoria) {
        if (id != precoCategoria.Id) {
            return BadRequest();
        }

        await _repository.UpdateAsync(precoCategoria);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaPreco>> PostPrecoCategoria(CategoriaPreco precoCategoria) {
        await _repository.AddAsync(precoCategoria);

        return CreatedAtAction("GetPrecoCategoria", new { id = precoCategoria.Id }, precoCategoria);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePrecoCategoria(int id) {
        var precoCategoria = await _repository.GetByIdAsync(id);
        if (precoCategoria == null) {
            return NotFound();
        }

        await _repository.DeleteAsync(id);

        return NoContent();
    }
}
