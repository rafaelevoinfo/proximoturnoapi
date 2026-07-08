namespace ProximoTurnoApi.Application.DTOs;

public record EnviarEmailsClientesRequest
{
    public List<int> ClienteIds { get; set; } = [];
    public string Titulo { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
}
