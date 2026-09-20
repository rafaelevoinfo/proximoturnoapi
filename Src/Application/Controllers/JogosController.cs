using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/jogos")]
[ApiController]
public class JogosController(ILogger<JogosController> logger,
                             IJogoRepository _repository,
                             CadastroJogo _cadastroJogoUseCase,
                             AtualizarJogo _atualizarJogoUseCase,
                             ObterJogo _obterJogoUseCase,
                             IIndexacaoManualRepository _indexacaoRepository,
                             IManualQueue _manualQueue) : ControllerBasico(logger) {

    /// <summary>
    /// Avisa a fila que os manuais deste jogo podem ter mudado de situação: desativar tira
    /// os manuais da busca, reativar devolve.
    /// </summary>
    private async Task SincronizarManuaisAsync(int idJogo) {
        try {
            foreach (var idLink in await _indexacaoRepository.GetIdsLinksAsync(idJogo)) {
                _manualQueue.Enfileirar(new ManualJob(idLink, idJogo));
            }
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            // Enfileirar e so um aviso: a operacao (desativar/reativar) ja foi salva antes
            // desta chamada. Perder o aviso aqui nao pode virar 500 para uma escrita que deu
            // certo, nem mentir que a operacao falhou; a proxima reconciliacao do worker acha
            // o link e corrige.
            _logger.LogError(ex, "Falha ao avisar a fila de indexação sobre o jogo {IdJogo}.", idJogo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetJogos([FromQuery] FiltroJogoDTO filtro) {
        _logger.LogInformation("Recuperando jogos.");
        return await EncapsulateRequestAsync(async () => {
            var jogos = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<JogoCardDTO>>.CreateSuccessResult(jogos.Select(JogoCardDTO.FromModel).ToList(), "Jogos recuperados com sucesso."));
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetJogosAdmin([FromQuery] FiltroJogoAdminDTO filtro) {
        _logger.LogInformation("Recuperando jogos (admin).");
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

    [HttpGet("novidades")]
    public async Task<IActionResult> GetNovidades() {
        _logger.LogInformation("Recuperando novidades.");
        return await EncapsulateRequestAsync(async () => {
            var jogos = await _repository.GetNovidadesAsync();
            return Ok(ApiResultDTO<List<JogoCardDTO>>.CreateSuccessResult(jogos.Select(JogoCardDTO.FromModel).ToList(), "Novidades recuperadas com sucesso."));
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetJogo([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var jogo = await _obterJogoUseCase.ObterJogoAsync(id);
            if (jogo == null) {
                return NotFound(ApiResultDTO<JogoPublicDTO>.CreateFailureResult($"Jogo de id {id} não encontrado."));
            }

            return Ok(ApiResultDTO<JogoPublicDTO>.CreateSuccessResult(jogo, "Jogo recuperado com sucesso."));
        });
    }

    [HttpGet("admin/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetJogoAdmin([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var jogo = await _repository.GetByIdAsync(id);
            if (jogo == null) {
                return NotFound(ApiResultDTO<JogoDTO>.CreateFailureResult($"Jogo de id {id} não encontrado."));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(JogoDTO.FromModel(jogo), "Jogo recuperado com sucesso."));
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PutJogo([FromRoute] int id, [FromBody] JogoDTO jogoDto) {
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
    public async Task<IActionResult> PostJogo([FromBody] JogoDTO jogoDto) {
        return await EncapsulateRequestAsync(async () => {
            var idJogo = await _cadastroJogoUseCase.ExecuteAsync(jogoDto);
            if (idJogo == 0) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(_cadastroJogoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<JogoDTO>.CreateSuccessResult(new JogoDTO() { Id = idJogo }, "Jogo criado com sucesso."));
        });
    }

    [HttpGet("{idJogo:int}/copias")]
    public async Task<IActionResult> GetCopias([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var copias = await _repository.GetCopiasAsync(idJogo);
            return Ok(ApiResultDTO<List<CopiaJogoDTO>>.CreateSuccessResult(copias.Select(CopiaJogoDTO.FromModel).ToList(), "Copias recuperadas com sucesso."));
        });
    }

    [HttpPost("{idJogo:int}/copia")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdicionarCopia([FromRoute] int idJogo) {
        return await EncapsulateRequestAsync(async () => {
            var idCopia = await _cadastroJogoUseCase.AdicionarCopia(idJogo);
            if (idCopia.GetValueOrDefault() == 0) {
                return BadRequest(ApiResultDTO<JogoDTO>.CreateFailureResult(_cadastroJogoUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<int>.CreateSuccessResult(idCopia.GetValueOrDefault(), "Copia adicionada com sucesso."));
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteJogo([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var copias = await _repository.GetAllCopiasByIdJogoAsync(id);
            if (copias is null || copias.Count == 0) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Nenhuma copia do jogo foi encontrada."));
            }
            foreach (var copia in copias) {
                copia.Status = StatusJogo.Desativado;
            }
            await _repository.SaveChangesAsync();
            await SincronizarManuaisAsync(id);

            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo desativado com sucesso."));
        });
    }

    [HttpDelete("{idJogo:int}/copia/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DesativarCopiaJogo([FromRoute] int idJogo, [FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var copia = await _repository.GetCopiaByIdAsync(id);
            if (copia == null) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Copia de id {id} não encontrada."));
            }
            copia.Status = StatusJogo.Desativado;
            await _repository.SaveAsync(copia);
            await SincronizarManuaisAsync(idJogo);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Copia excluída com sucesso."));
        });
    }

    [HttpPut("{id:int}/reativar")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ReativarJogo([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var copias = await _repository.GetAllCopiasByIdJogoAsync(id);
            if (copias is null || copias.Count == 0) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Nenhuma copia do jogo foi encontrada."));
            }
            foreach (var copia in copias.Where(c => c.Status == StatusJogo.Desativado)) {
                copia.Status = StatusJogo.Disponivel;
            }
            await _repository.SaveChangesAsync();
            await SincronizarManuaisAsync(id);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Jogo reativado com sucesso."));
        });
    }

    [HttpPut("{idJogo:int}/copia/{id:int}/reativar")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ReativarCopiaJogo([FromRoute] int idJogo, [FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var copia = await _repository.GetCopiaByIdAsync(id);
            if (copia == null || copia.IdJogo != idJogo) {
                return NotFound(ApiResultDTO<object>.CreateFailureResult($"Copia de id {id} não encontrada."));
            }
            if (copia.Status == StatusJogo.Desativado) {
                copia.Status = StatusJogo.Disponivel;
                await _repository.SaveAsync(copia);
            }
            await SincronizarManuaisAsync(idJogo);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(null, "Copia reativada com sucesso."));
        });
    }

}
