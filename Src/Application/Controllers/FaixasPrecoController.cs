using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases.FaixaPreco;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/faixas-preco")]
[ApiController]
public class FaixasPrecoController(ILogger<ControllerBasico> logger, IFaixaPrecoRepository _repository) : ControllerBasico(logger)
{

    [HttpGet]
    public async Task<IActionResult> GetFaixasPreco([FromQuery] FiltroFaixaPrecoDTO filtro)
    {
        _logger.LogInformation("Recuperando faixas de preço.");
        return await EncapsulateRequestAsync(async () =>
        {
            // TODO: Implement filtering in repository
            var faixas = await _repository.GetAllAsync();
            return Ok(ApiResultDTO<List<FaixaPrecoDTO>>.CreateSuccessResult(faixas.Select(FaixaPrecoDTO.FromModel).ToList(), "Faixas de preço recuperadas com sucesso."));
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFaixaPreco(int id)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var faixa = await _repository.GetByIdAsync(id);
            if (faixa == null)
            {
                return NotFound(ApiResultDTO<FaixaPrecoDTO>.CreateFailureResult($"Faixa de preço de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<FaixaPrecoDTO>.CreateSuccessResult(FaixaPrecoDTO.FromModel(faixa), "Faixa de preço recuperada com sucesso."));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutFaixaPreco(int id, FaixaPrecoDTO faixaDto)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            if (id != faixaDto.Id)
            {
                return BadRequest(ApiResultDTO<FaixaPrecoDTO>.CreateFailureResult("ID da faixa de preço na URL não corresponde ao ID no corpo da requisição."));
            }
            var atualizarFaixaPreco = new AtualizarFaixaPreco(_repository);
            var result = await atualizarFaixaPreco.ExecuteAsync(faixaDto);
            if (!result)
            {
                return BadRequest(ApiResultDTO<FaixaPrecoDTO>.CreateFailureResult(atualizarFaixaPreco.AggregateErrors()));
            }
            return Ok(ApiResultDTO<FaixaPrecoDTO>.CreateSuccessResult(null, "Faixa de preço atualizada com sucesso."));
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostFaixaPreco(FaixaPrecoDTO faixaDto)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            var cadastroFaixaPreco = new CadastroFaixaPreco(_repository);
            var idFaixa = await cadastroFaixaPreco.ExecuteAsync(faixaDto);
            if (idFaixa == 0)
            {
                return BadRequest(ApiResultDTO<FaixaPrecoDTO>.CreateFailureResult(cadastroFaixaPreco.AggregateErrors()));
            }
            return Ok(ApiResultDTO<FaixaPrecoDTO>.CreateSuccessResult(new FaixaPrecoDTO() { Id = idFaixa }, "Faixa de preço criada com sucesso."));
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFaixaPreco(int id)
    {
        return await EncapsulateRequestAsync(async () =>
        {
            if (!await _repository.DeleteAsync(id))
            {
                return NotFound(ApiResultDTO<FaixaPrecoDTO>.CreateFailureResult($"Faixa de preço de id {id} não encontrada."));
            }
            return Ok(ApiResultDTO<FaixaPrecoDTO>.CreateSuccessResult(null, "Faixa de preço excluída com sucesso."));
        });
    }
}