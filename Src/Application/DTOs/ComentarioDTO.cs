using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public class ComentarioDTO {
    public int Id { get; set; }
    public int IdJogo { get; set; }
    public string NomeJogo { get; set; } = null!;
    public int IdCliente { get; set; }
    public string NomeCliente { get; set; } = null!;
    public string Texto { get; set; } = null!;
    public short Nota { get; set; }
    public DateTime DataHora { get; set; }
    public StatusComentario Status { get; set; }

    public static ComentarioDTO FromModel(Comentario model) {
        return new ComentarioDTO {
            Id = model.Id,
            IdJogo = model.IdJogo,
            NomeJogo = model.Jogo?.Nome ?? "Jogo Desconhecido",
            IdCliente = model.IdCliente,
            NomeCliente = model.Cliente?.Nome ?? "Cliente Anônimo",
            Texto = model.Texto,
            Nota = model.Nota,
            DataHora = model.DataHora,
            Status = model.Status
        };
    }
}
