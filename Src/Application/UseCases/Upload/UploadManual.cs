using Flunt.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProximoTurnoApi.Application.UseCases;

public class UploadManual(IWebHostEnvironment _env, ILogger<UploadManual> logger) : UseCaseBasico {
    public async Task<string?> ExecuteAsync(IFormFile? file, string baseUrl) {
        if (file == null || file.Length == 0) {
            logger.LogWarning("Falha no upload: Nenhum arquivo enviado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum arquivo enviado."));
            return null;
        }

        const long maxFileSize = 52428800; // 50MB
        if (file.Length > maxFileSize) {
            logger.LogWarning("Falha no upload: O arquivo excede o limite de tamanho de 50MB.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O arquivo excede o limite de tamanho de 50MB."));
            return null;
        }

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".txt" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension)) {
            logger.LogWarning("Falha no upload: Extensão {Extension} não permitida.", extension);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Extensão de arquivo não permitida."));
            return null;
        }

        try {
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
            if (!Directory.Exists(uploadsFolder)) {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Create a unique filename to prevent collisions
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(fileStream);
            }

            var fileUrl = $"{baseUrl}/uploads/{uniqueFileName}";
            logger.LogInformation("Arquivo {FileName} salvo com sucesso no servidor. URL: {FileUrl}", file.FileName, fileUrl);
            return fileUrl;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao processar upload do arquivo.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Erro interno ao salvar arquivo."));
            return null;
        }
    }
}
