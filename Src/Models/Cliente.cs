
namespace ProximoTurnoApi.Models;

public class Cliente {
    public int Id { get; set; }
    public required string NomeCompleto { get; set; }
    public required string Telefone { get; set; }
    public required string Endereco { get; set; }
    public required string Email { get; set; }
    public DateTime? DataNascimento { get; set; }
    public string? ComoNosConheceu { get; set; }
    public bool AceitaReceberOfertas { get; set; }
}

