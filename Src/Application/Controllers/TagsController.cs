using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/tags")]
[ApiController]
public class TagsController(ILogger<ControllerBasico> logger,
                            ITagRepository _repository,
                            CadastroTag _cadastroTagUseCase,
                            AtualizarTag _atualizarTagUseCase) : ControllerBasico(logger) {

    [HttpGet]
    public async Task<IActionResult> GetTags(FiltroTagDTO filtro) {
        _logger.LogInformation("Recuperando tags.");
        return await EncapsulateRequestAsync(async () => {
            var tags = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<TagDTO>>.CreateSuccessResult(tags.Select(TagDTO.FromModel).ToList(), "Tags recuperadas com sucesso."));
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTag([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            var tag = await _repository.GetByIdAsync(id);
            if (tag == null) {
                return NotFound(ApiResultDTO<TagDTO>.CreateFailureResult($"Tag de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<TagDTO>.CreateSuccessResult(TagDTO.FromModel(tag), "Tag recuperada com sucesso."));
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PutTag([FromRoute] int id, [FromBody] TagDTO tag) {
        return await EncapsulateRequestAsync(async () => {
            if (id != tag.Id) {
                return BadRequest(ApiResultDTO<TagDTO>.CreateFailureResult("ID da tag na URL não corresponde ao ID no corpo da requisição."));
            }
            var result = await _atualizarTagUseCase.ExecuteAsync(tag);
            if (!result) {
                return BadRequest(ApiResultDTO<TagDTO>.CreateFailureResult(_atualizarTagUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<TagDTO>.CreateSuccessResult(null, "Tag atualizada com sucesso."));
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PostTag([FromBody] TagDTO tagDto) {
        return await EncapsulateRequestAsync(async () => {
            var idTag = await _cadastroTagUseCase.ExecuteAsync(tagDto);
            if (idTag == 0) {
                return BadRequest(ApiResultDTO<TagDTO>.CreateFailureResult(_cadastroTagUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<TagDTO>.CreateSuccessResult(new TagDTO() { Id = idTag }, "Tag criada com sucesso."));
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTag([FromRoute] int id) {
        return await EncapsulateRequestAsync(async () => {
            if (!await _repository.DeleteAsync(id)) {
                return NotFound(ApiResultDTO<TagDTO>.CreateFailureResult($"Tag de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<TagDTO>.CreateSuccessResult(null, "Tag excluída com sucesso."));
        });
    }
}
