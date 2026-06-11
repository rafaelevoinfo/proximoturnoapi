using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/cupons")]
[ApiController]
[Authorize]
public class CuponsController(
    ILogger<ControllerBasico> logger,
    ListarCupons _listarCupons,
    CadastroCupom _cadastroCupom,
    AtualizarCupom _atualizarCupom,
    ValidarCupom _validarCupom) : ControllerBasico(logger)
{
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetAll([FromQuery] FiltroCupomDTO filtro) =>
        await EncapsulateRequestAsync(async () =>
        {
            var result = await _listarCupons.ExecuteAsync(filtro);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(result, "Cupons listados com sucesso."));
        });

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create([FromBody] NovoCupomDTO dto) =>
        await EncapsulateRequestAsync(async () =>
        {
            var result = await _cadastroCupom.ExecuteAsync(dto);
            if (!_cadastroCupom.IsValid)
                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_cadastroCupom.AggregateErrors()));

            return Ok(ApiResultDTO<object>.CreateSuccessResult(result, "Cupom criado com sucesso."));
        });

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] NovoCupomDTO dto) =>
        await EncapsulateRequestAsync(async () =>
        {
            dto.Id = id;
            var result = await _atualizarCupom.ExecuteAsync(dto);
            if (!_atualizarCupom.IsValid)
            {
                if (_atualizarCupom.Notifications.Any(n => n.Type == UseCaseNotificationType.NotFound))
                    return NotFound(ApiResultDTO<object>.CreateFailureResult(_atualizarCupom.AggregateErrors()));

                return BadRequest(ApiResultDTO<object>.CreateFailureResult(_atualizarCupom.AggregateErrors()));
            }

            return Ok(ApiResultDTO<object>.CreateSuccessResult(result, "Cupom atualizado com sucesso."));
        });

    [HttpPost("validar")]
    public async Task<IActionResult> Validar([FromBody] ValidarCupomDTO dto) =>
        await EncapsulateRequestAsync(async () =>
        {
            var result = await _validarCupom.ExecuteAsync(dto);
            return Ok(ApiResultDTO<object>.CreateSuccessResult(result, "Validação concluída."));
        });
}
