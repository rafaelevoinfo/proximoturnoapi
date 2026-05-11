using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/jogos")]
[ApiController]
public class JogosController(ILogger<ControllerBasico> logger,
                             IJogoRepository _repository,
                             ITagRepository _tagRepository,
                             CadastroJogo _cadastroJogoUseCase,
                             AtualizarJogo _atualizarJogoUseCase) : ControllerBasico(logger) {

    [HttpGet]
    public async Task<IActionResult> GetJogos([FromQuery] FiltroJogoDTO filtro) {
        _logger.LogInformation("Recuperando jogos.");
        return await EncapsulateRequestAsync(async () => {
            var jogos = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<JogoCardDTO>>.CreateSuccessResult(jogos.Select(JogoCardDTO.FromModel).ToList(), "Jogos recuperados com sucesso."));
        });
    }

    [HttpGet("mais-alugados")]
    public async Task<IActionResult> GetJogosMaisAlugados([FromQuery] FiltroJogoDTO filtro) {
        _logger.LogInformation("Recuperando jogos mais alugados.");
        return await EncapsulateRequestAsync(async () => {
            var jogos = await _repository.GetMaisAlugadosAsync();
            return Ok(ApiResultDTO<List<JogoCardDTO>>.CreateSuccessResult(jogos.Select(JogoCardDTO.FromModel).ToList(), "Jogos mais alugados recuperados com sucesso."));
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
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PutJogo(int id, JogoDTO jogoDto) {
        return await EncapsulateRequestAsync(async () => {
            if (id != jogoDto.Id) {
                return BadRequest(ApiResultDTO<object>.CreateFailureResult("ID do jogo na URL não corresponde ao ID no corpo da requisição."));
            }

            var result = await _atualizarJogoUseCase.ExecuteAsync(jogoDto);
            if (!result) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(_atualizarJogoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(null, "Jogo atualizado com sucesso."));
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PostJogo(JogoDTO jogoDto) {
        return await EncapsulateRequestAsync(async () => {
            var idJogo = await _cadastroJogoUseCase.ExecuteAsync(jogoDto);
            if (idJogo == 0) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(_cadastroJogoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(new JogoDTO() { Id = idJogo }, "Jogo criado com sucesso."));
        });
    }

    [HttpGet("{idJogo}/copias")]
    public async Task<IActionResult> GetCopias(int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var copias = await _repository.GetCopiasAsync(idJogo);
            return Ok(ApiResultDTO<List<CopiaJogoDTO>>.CreateSuccessResult(copias.Select(CopiaJogoDTO.FromModel).ToList(), "Copias recuperadas com sucesso."));
        });
    }

    [HttpPost("{idJogo}/copia")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdicionarCopia(int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var idCopia = await _cadastroJogoUseCase.AdicionarCopia(idJogo);
            if (idCopia.GetValueOrDefault() == 0) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(_cadastroJogoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<int>.CreateSuccessResult(idCopia.GetValueOrDefault(), "Copia adicionada com sucesso."));
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteJogo(int id) {
        return await EncapsulateRequestAsync(async () => {
            var copias = await _repository.GetAllCopiasByIdJogoAsync(id);
            if (copias is null || copias.Count == 0) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Nenhuma copia do jogo foi encontrada."));
            }
            foreach (var copia in copias) {
                copia.Status = StatusJogo.Desativado;
            }
            await _repository.SaveChangesAsync();

            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo desativado com sucesso."));
        });
    }

    [HttpDelete("{idJogo}/copia/{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DesativarCopiaJogo(int id) {
        return await EncapsulateRequestAsync(async () => {
            var copia = await _repository.GetCopiaByIdAsync(id);
            if (copia == null) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Copia de id {id} não encontrada."));
            }
            copia.Status = StatusJogo.Desativado;
            await _repository.SaveAsync(copia);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Copia excluída com sucesso."));
        });
    }

}
