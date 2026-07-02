using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using Microsoft.AspNetCore.Hosting;

namespace ProximoTurnoApi.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UploadController(ILogger<UploadController> logger, 
                            CloudinaryService _cloudinaryService, 
                            IWebHostEnvironment _env,
                            UploadManual _uploadManualUseCase) : ControllerBasico(logger) {
    
    [HttpGet("signature")]
    public IActionResult GetSignature([FromQuery] string? tipo = null) {
        var baseFolder = _env.IsDevelopment() ? "proximoturno/jogos_debug" : "proximoturno/jogos";
        var folder = tipo == "manuais" ? $"{baseFolder}/manuais" : baseFolder;
        var signature = _cloudinaryService.GetSignature(folder);
        return Ok(signature);
    }

    [HttpPost("manual")]
    [RequestSizeLimit(52428800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<IActionResult> UploadManual(IFormFile file) {
        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        
        var fileUrl = await _uploadManualUseCase.ExecuteAsync(file, baseUrl);
        if (fileUrl == null) {
            return BadRequest(ApiResultDTO<string>.CreateFailureResult(_uploadManualUseCase.AggregateErrors()));
        }
        
        return Ok(ApiResultDTO<string>.CreateSuccessResult(fileUrl, "Upload realizado com sucesso."));
    }
}
