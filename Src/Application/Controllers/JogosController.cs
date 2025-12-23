using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.Jogo;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/jogos")]
[ApiController]
public class JogosController(ILogger<ControllerBasico> logger, IJogoRepository _repository) : ControllerBasico(logger) {

    [HttpGet]
    public async Task<IActionResult> GetJogos([FromQuery] FiltroJogoDTO filtro) {
        _logger.LogInformation("Recuperando jogos.");
        return await EncapsulateRequestAsync(async () => {
            var jogos = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<JogoDTO>>.CreateSuccessResult(jogos.Select(JogoDTO.FromModel).ToList(), "Jogos recuperados com sucesso."));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJogo(int id) {
        return await EncapsulateRequestAsync(async () => {
            var jogo = await _repository.GetByIdAsync(id);
            if (jogo == null) {
                return NotFound(ApiResultDTO<JogoDTO>.CreateFailureResult($"Jogo de id {id} não encontrado."));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(JogoDTO.FromModel(jogo), "Jogo recuperado com sucesso."));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutJogo(int id, JogoDTO jogoDto) {
        return await EncapsulateRequestAsync(async () => {
            if (id != jogoDto.Id) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult("ID do jogo na URL não corresponde ao ID no corpo da requisição."));
            }

            var atualizarJogo = new AtualizarJogo(_repository);
            var result = await atualizarJogo.ExecuteAsync(jogoDto);
            if (!result) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(atualizarJogo.AggregateErrors()));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(null, "Jogo atualizado com sucesso."));
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostJogo(JogoDTO jogoDto) {
        return await EncapsulateRequestAsync(async () => {
            var cadastroJogo = new CadastroJogo(_repository);
            var idJogo = await cadastroJogo.ExecuteAsync(jogoDto);
            if (idJogo == 0) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(cadastroJogo.AggregateErrors()));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(new JogoDTO() { Id = idJogo }, "Jogo criado com sucesso."));
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJogo(int id) {
        return await EncapsulateRequestAsync(async () => {
            var jogo = await _repository.GetByIdAsync(id);
            if (jogo == null) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Jogo de id {id} não encontrado."));
            }
            await _repository.DeleteAsync(id);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo excluído com sucesso."));
        });
    }
}
