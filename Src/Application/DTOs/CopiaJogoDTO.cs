using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record CopiaJogoDTO {
    public int Id { get; set; }
    public StatusJogo Status { get; set; }

    public static CopiaJogoDTO FromModel(JogoCopia copia) {
        return new CopiaJogoDTO() {
            Id = copia.Id,
            Status = copia.Status
        };
    }
}
