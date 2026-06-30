using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UploadController(ILogger<UploadController> logger, CloudinaryService _cloudinaryService, IWebHostEnvironment _env) : ControllerBasico(logger) {
    
    [HttpGet("signature")]
    public IActionResult GetSignature([FromQuery] string? tipo = null) {
        var baseFolder = _env.IsDevelopment() ? "proximoturno/jogos_debug" : "proximoturno/jogos";
        var folder = tipo == "manuais" ? $"{baseFolder}/manuais" : baseFolder;
        var signature = _cloudinaryService.GetSignature(folder);
        return Ok(signature);
    }
}
