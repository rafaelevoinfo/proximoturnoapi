namespace ProximoTurnoApi.Application.DTOs;

/// <summary>A <c>PushSubscription</c> do navegador, já com as chaves extraídas.</summary>
public record InscricaoPushDTO {
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

public record RemoverInscricaoPushDTO {
    public string Endpoint { get; set; } = string.Empty;
}
