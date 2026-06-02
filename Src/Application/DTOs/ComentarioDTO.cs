using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class ComentarioDTO
{
    public int Id { get; set; }
    public int IdPedido { get; set; }
    public int IdJogo { get; set; }
    public string NomeCliente { get; set; } = null!;
    public string Texto { get; set; } = null!;
    public int Nota { get; set; }
    public DateTime DataHora { get; set; }

    public static ComentarioDTO FromModel(Comentario model)
    {
        return new ComentarioDTO
        {
            Id = model.Id,
            IdPedido = model.IdPedido,
            IdJogo = model.IdJogo,
            NomeCliente = model.Cliente?.Nome ?? "Cliente Anônimo",
            Texto = model.Texto,
            Nota = model.Nota,
            DataHora = model.DataHora
        };
    }
}
