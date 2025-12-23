using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class ClienteDTO {
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public required string Nome { get; set; }
    [Required, MaxLength(15)]
    public required string Telefone { get; set; }

    [Required, MaxLength(400)]
    public required string Endereco { get; set; }

    [Required, MaxLength(100)]
    public required string Email { get; set; }

    public DateOnly? DataNascimento { get; set; }

    public string? ComoNosConheceu { get; set; }
    public bool AceitaReceberOfertas { get; set; }

    public Cliente ToModel() {
        return new Cliente {
            Id = Id ?? 0,
            Nome = Nome,
            Telefone = Telefone,
            Endereco = Endereco,
            Email = Email,
            DataNascimento = DataNascimento,
            ComoNosConheceu = ComoNosConheceu,
            AceitaReceberOfertas = AceitaReceberOfertas
        };
    }

    public Cliente ToModel(Cliente cliente) {
        cliente.Nome = Nome;
        cliente.Telefone = Telefone;
        cliente.Endereco = Endereco;
        cliente.Email = Email;
        cliente.DataNascimento = DataNascimento;
        cliente.ComoNosConheceu = ComoNosConheceu;
        cliente.AceitaReceberOfertas = AceitaReceberOfertas;
        return cliente;
    }

    public static ClienteDTO FromModel(Cliente cliente) {
        return new ClienteDTO {
            Id = cliente.Id,
            Nome = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(cliente.Nome),
            Telefone = cliente.Telefone,
            Endereco = cliente.Endereco,
            Email = cliente.Email,
            DataNascimento = cliente.DataNascimento,
            ComoNosConheceu = cliente.ComoNosConheceu,
            AceitaReceberOfertas = cliente.AceitaReceberOfertas
        };
    }
}
