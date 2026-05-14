using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UploadController(ILogger<UploadController> logger, CloudinaryService _cloudinaryService) : ControllerBasico(logger) {
    
    [HttpGet("signature")]
    public IActionResult GetSignature() {
        var signature = _cloudinaryService.GetSignature("proximoturno/jogos");
        return Ok(signature);
    }
}
