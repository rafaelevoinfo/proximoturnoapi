namespace ProximoTurnoApi.Application.DTOs;

public class SalvarComentarioDTO {
    public int? Id { get; set; }
    public int IdJogo { get; set; }
    public string Texto { get; set; } = null!;
    public short Nota { get; set; }
}
