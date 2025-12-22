using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Models;

[Table("CATEGORIA_PRECO")]
public class CategoriaPreco {
    [Column("ID")]
    public int Id { get; set; }
    [Column("ID_CATEGORIA")]
    public int IdCategoria { get; set; }
    public Categoria? Categoria { get; set; }
    [Column("QUANTIDADE_DIAS")]
    public int QuantidadeDias { get; set; }
    [Column("VALOR")]
    public decimal Valor { get; set; }
}
