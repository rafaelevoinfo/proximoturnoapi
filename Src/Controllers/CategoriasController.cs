using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;

namespace ProximoTurnoApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaRepository _repository;

    public CategoriasController(ICategoriaRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Categoria>>> GetCategorias()
    {
        return await _repository.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Categoria>> GetCategoria(int id)
    {
        var categoria = await _repository.GetByIdAsync(id);

        if (categoria == null)
        {
            return NotFound();
        }

        return categoria;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCategoria(int id, Categoria categoria)
    {
        if (id != categoria.Id)
        {
            return BadRequest();
        }

        await _repository.UpdateAsync(categoria);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Categoria>> PostCategoria(Categoria categoria)
    {
        await _repository.AddAsync(categoria);

        return CreatedAtAction("GetCategoria", new { id = categoria.Id }, categoria);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategoria(int id)
    {
        var categoria = await _repository.GetByIdAsync(id);
        if (categoria == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id);

        return NoContent();
    }
}
