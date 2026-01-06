
namespace ProximoTurnoApi.Infrastructure.Models;

public class CategoriaPeriodo : BaseModel {
    public Categoria Categoria { get; set; } = null!;
    public int QuantidadeDias { get; set; }
    public decimal Valor { get; set; }
}