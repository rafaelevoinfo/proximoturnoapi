using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;

namespace ProximoTurnoApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClientesController : ControllerBase
{
    private readonly IClienteRepository _repository;

    public ClientesController(IClienteRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
    {
        return await _repository.GetAllAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Cliente>> GetCliente(int id)
    {
        var cliente = await _repository.GetByIdAsync(id);

        if (cliente == null)
        {
            return NotFound();
        }

        return cliente;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCliente(int id, Cliente cliente)
    {
        if (id != cliente.Id)
        {
            return BadRequest();
        }

        await _repository.UpdateAsync(cliente);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Cliente>> PostCliente(Cliente cliente)
    {
        await _repository.AddAsync(cliente);

        return CreatedAtAction("GetCliente", new { id = cliente.Id }, cliente);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var cliente = await _repository.GetByIdAsync(id);
        if (cliente == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id);

        return NoContent();
    }
}
