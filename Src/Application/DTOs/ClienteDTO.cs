using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record ClienteResumoDTO {
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public static ClienteResumoDTO FromModel(Cliente cliente) {
        return new ClienteResumoDTO {
            Id = cliente.Id,
            Nome = cliente.Nome,
        };
    }
}

public record ClienteDTO {
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Nome { get; set => field = StringUtils.Capitalize(value); } = string.Empty;
    [Required, MaxLength(15)]
    public string Telefone { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string Endereco { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

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

    public void UpdateModel(Cliente cliente) {
        cliente.Nome = Nome;
        cliente.Telefone = Telefone;
        cliente.Endereco = Endereco;
        cliente.Email = Email;
        cliente.DataNascimento = DataNascimento;
        cliente.ComoNosConheceu = ComoNosConheceu;
        cliente.AceitaReceberOfertas = AceitaReceberOfertas;
    }

    public static ClienteDTO FromModel(Cliente cliente) {
        return new ClienteDTO {
            Id = cliente.Id,
            Nome = cliente.Nome,
            Telefone = cliente.Telefone,
            Endereco = cliente.Endereco,
            Email = cliente.Email,
            DataNascimento = cliente.DataNascimento,
            ComoNosConheceu = cliente.ComoNosConheceu,
            AceitaReceberOfertas = cliente.AceitaReceberOfertas
        };
    }
}
