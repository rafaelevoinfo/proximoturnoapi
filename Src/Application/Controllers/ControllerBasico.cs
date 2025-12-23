using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Application.Controllers;

public abstract class ControllerBasico(ILogger logger) : ControllerBase {
    protected readonly ILogger _logger = logger;

    protected async Task<IActionResult> EncapsulateRequestAsync(Func<Task<IActionResult>> func) {
        try {
            return await func();
        } catch (InvalidOperationException ex) {
            _logger.LogWarning(ex, "Operação inválida");

            return BadRequest(new ApiResultDTO<string> {
                Success = false,
                Message = ex.Message,
            });
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro interno no servidor");

            return StatusCode(500, new ApiResultDTO<string> {
                Success = false,
                Message = "Erro interno no servidor",
            });
        }
    }
}