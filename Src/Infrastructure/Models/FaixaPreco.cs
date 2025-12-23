using System.ComponentModel.DataAnnotations.Schema;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Models;

[Table("FAIXA_PRECO")]
public class FaixaPreco
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("QUANTIDADE_DIAS")]
    public int QuantidadeDias { get; set; }

    [Column("VALOR")]
    public decimal Valor { get; set; }

    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
}