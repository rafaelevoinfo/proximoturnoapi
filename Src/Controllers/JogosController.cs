using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Repositories;

namespace ProximoTurnoApi.Controllers;

[ApiController]
[Route("[controller]")]
public class JogosController : ControllerBase{
    private readonly IJogoRepository _jogoRepository;

    public JogosController(IJogoRepository jogoRepository){
        _jogoRepository = jogoRepository;
    }

    [HttpGet]
    public IEnumerable<Jogo> Get(){
        return _jogoRepository.GetAll();
    }

    [HttpGet("{id}")]
    public ActionResult<Jogo> Get(int id){
        var jogo = _jogoRepository.GetById(id);
        if (jogo == null){
            return NotFound();
        }
        return jogo;
    }

    [HttpPost]
    public Jogo Post([FromBody] Jogo jogo){
        return _jogoRepository.Add(jogo);
    }

    [HttpPut("{id}")]
    public IActionResult Put(int id, [FromBody] Jogo jogo){
        if (id != jogo.Id){
            return BadRequest();
        }
        _jogoRepository.Update(jogo);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id){
        var jogo = _jogoRepository.GetById(id);
        if (jogo == null){
            return NotFound();
        }
        _jogoRepository.Delete(id);
        return NoContent();
    }
}