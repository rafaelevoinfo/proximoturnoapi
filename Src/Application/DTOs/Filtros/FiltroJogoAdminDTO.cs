using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroJogoAdminDTO : FiltroJogoDTO {
    [FromQuery(Name = "id_categoria")]
    public int? IdCategoria { get; set; }
    [FromQuery(Name = "status")]
    public StatusJogo? Status { get; set; }
}
