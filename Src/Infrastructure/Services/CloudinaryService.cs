using CloudinaryDotNet;
using Microsoft.Extensions.Configuration;

namespace ProximoTurnoApi.Infrastructure.Services;

public record CloudinarySignatureDTO(string Signature, string ApiKey, long Timestamp, string CloudName, string Folder);

public class CloudinaryService {
    private readonly Cloudinary _cloudinary;
    private readonly string _cloudName;
    private readonly string _apiKey;

    public CloudinaryService(IConfiguration configuration) {
        var url = configuration["CLOUDINARY_URL"];
        if (string.IsNullOrEmpty(url)) throw new Exception("CLOUDINARY_URL missing.");
        
        _cloudinary = new Cloudinary(url);
        _cloudName = _cloudinary.Api.Account.Cloud;
        _apiKey = _cloudinary.Api.Account.ApiKey;
    }

    public CloudinarySignatureDTO GetSignature(string folder) {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var parameters = new Dictionary<string, object> {
            { "folder", folder },
            { "timestamp", timestamp }
        };

        var signature = _cloudinary.Api.SignParameters(parameters);
        return new CloudinarySignatureDTO(signature, _apiKey, timestamp, _cloudName, folder);
    }
}
