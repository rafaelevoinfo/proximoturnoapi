using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public class ComentarioFiltersDTO
{
    public DateTime? DataInicial { get; set; }
    public DateTime? DataFinal { get; set; }
    public StatusComentario? Status { get; set; }
}
