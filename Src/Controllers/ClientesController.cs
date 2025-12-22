using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;
using System.Collections.Generic;

namespace ProximoTurnoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientesController : ControllerBase{
    private readonly IClienteRepository _clienteRepository;

    public ClientesController(IClienteRepository clienteRepository){
        _clienteRepository = clienteRepository;
    }

    // GET: api/clientes
    [HttpGet]
    public ActionResult<IEnumerable<Cliente>> GetClientes(){
        return Ok(_clienteRepository.GetAll());
    }

    // GET: api/clientes/5
    [HttpGet("{id}")]
    public ActionResult<Cliente> GetCliente(int id){
        var cliente = _clienteRepository.GetById(id);
        if (cliente == null){
            return NotFound();
        }
        return Ok(cliente);
    }

    // POST: api/clientes
    [HttpPost]
    public ActionResult<Cliente> PostCliente(Cliente cliente){
        var createdCliente = _clienteRepository.Add(cliente);
        return CreatedAtAction(nameof(GetCliente), new { id = createdCliente.Id }, createdCliente.Id);
    }

    // PUT: api/clientes/5
    [HttpPut("{id}")]
    public IActionResult PutCliente(int id, Cliente cliente){
        if (id != cliente.Id){
            return BadRequest();
        }

        var existingCliente = _clienteRepository.GetById(id);
        if (existingCliente == null){
            return NotFound();
        }

        _clienteRepository.Update(cliente);

        return NoContent();
    }

    // DELETE: api/clientes/5
    [HttpDelete("{id}")]
    public IActionResult DeleteCliente(int id){
        var cliente = _clienteRepository.GetById(id);
        if (cliente == null){
            return NotFound();
        }

        _clienteRepository.Delete(id);
        return NoContent();
    }
}