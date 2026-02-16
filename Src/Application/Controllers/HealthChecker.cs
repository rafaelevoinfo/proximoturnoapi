using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Controllers;

[Route("api/health")]
public class HealthCheckerController(ILogger<ControllerBasico> logger, IClienteRepository _repository) : ControllerBasico(logger) {

    [HttpGet]
    public IActionResult GetHealth() {
        return Ok(new ApiResultDTO<string> {
            Success = true,
            Message = "API está funcionando corretamente.",
        });
    }


}
