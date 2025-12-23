using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.Categoria;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/categorias")]
[ApiController]
public class CategoriasController(ILogger<ControllerBasico> logger, ICategoriaRepository _repository) : ControllerBasico(logger) {

    [HttpGet]
    public async Task<IActionResult> GetCategorias(FiltroCategoriaDTO filtro) {
        _logger.LogInformation("Recuperando categorias.");
        return await EncapsulateRequestAsync(async () => {
            var categorias = await _repository.GetAllAsync(filtro);
            return Ok(ApiResultDTO<List<CategoriaDTO>>.CreateSuccessResult([.. categorias.Select(CategoriaDTO.FromModel)], "Categorias recuperadas com sucesso."));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoria(int id) {
        return await EncapsulateRequestAsync(async () => {
            var categoria = await _repository.GetByIdAsync(id);
            if (categoria == null) {
                return NotFound(ApiResultDTO<CategoriaDTO>.CreateFailureResult($"Categoria de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<CategoriaDTO>.CreateSuccessResult(CategoriaDTO.FromModel(categoria), "Categoria recuperada com sucesso."));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCategoria(int id, CategoriaDTO categoria) {
        return await EncapsulateRequestAsync(async () => {
            if (id != categoria.Id) {
                return BadRequest(ApiResultDTO<CategoriaDTO>.CreateFailureResult("ID da categoria na URL não corresponde ao ID no corpo da requisição."));
            }
            var atualizarCategoria = new AtualizarCategoria(_repository);
            var result = await atualizarCategoria.ExecuteAsync(categoria);
            if (!result) {
                return BadRequest(ApiResultDTO<CategoriaDTO>.CreateFailureResult(atualizarCategoria.AggregateErrors()));
            }
            return Ok(ApiResultDTO<CategoriaDTO>.CreateSuccessResult(null, "Categoria atualizada com sucesso."));
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostCategoria(CategoriaDTO categoriaDto) {
        return await EncapsulateRequestAsync(async () => {
            var cadastroCategoriaUseCase = new CadastroCategoria(_repository);
            var idCategoria = await cadastroCategoriaUseCase.ExecuteAsync(categoriaDto);
            if (idCategoria == 0) {
                return BadRequest(ApiResultDTO<CategoriaDTO>.CreateFailureResult(cadastroCategoriaUseCase.AggregateErrors()));
            }
            return Ok(ApiResultDTO<CategoriaDTO>.CreateSuccessResult(new CategoriaDTO() { Id = idCategoria }, "Categoria criada com sucesso."));
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategoria(int id) {
        return await EncapsulateRequestAsync(async () => {
            if (!await _repository.DeleteAsync(id)) {
                return NotFound(ApiResultDTO<CategoriaDTO>.CreateFailureResult($"Categoria de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<CategoriaDTO>.CreateSuccessResult(null, "Categoria excluída com sucesso."));
        });
    }
}
