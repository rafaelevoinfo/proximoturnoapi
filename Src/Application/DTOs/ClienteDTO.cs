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
    private string _nome = null!;
    private string? _cpf;
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Nome { get => _nome; set => _nome = StringUtils.Capitalize(value); }
    [Required, MaxLength(15)]
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "O telefone deve conter apenas números, incluindo o DDD, com 10 ou 11 dígitos.")]
    public string Telefone { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string Endereco { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    [EmailAddress(ErrorMessage = "O e-mail informado não é válido.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Aceita com ou sem máscara; é sempre persistido apenas com os 11 dígitos.</summary>
    [CpfValido(ErrorMessage = "O CPF informado não é válido.")]
    public string? Cpf { get => _cpf; set => _cpf = CpfUtils.Normalizar(value); }

    public DateOnly? DataNascimento { get; set; }

    public string? ComoNosConheceu { get; set; }
    public bool AceitaReceberOfertas { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Indica se o usuário do Identity consegue efetuar login: existe, já definiu uma senha
    /// e não está bloqueado por lockout. Preenchido pelo controller, não pelo repositório.
    /// </summary>
    public bool LoginAtivo { get; set; }

    /// <summary>Somente leitura: quando a conta foi excluída pelo titular. Null = conta ativa.</summary>
    public DateTime? DataAnonimizacao { get; set; }

    public Cliente ToModel() {
        return new Cliente {
            Id = Id ?? 0,
            Nome = Nome,
            Telefone = Telefone,
            Endereco = Endereco,
            Email = Email,
            Cpf = Cpf,
            DataNascimento = DataNascimento,
            ComoNosConheceu = ComoNosConheceu,
            AceitaReceberOfertas = AceitaReceberOfertas,
            Ativo = Ativo
        };
    }

    public void UpdateModel(Cliente cliente) {
        cliente.Nome = Nome;
        cliente.Telefone = Telefone;
        cliente.Endereco = Endereco;
        cliente.Cpf = Cpf;
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
            Cpf = cliente.Cpf,
            DataNascimento = cliente.DataNascimento,
            ComoNosConheceu = cliente.ComoNosConheceu,
            AceitaReceberOfertas = cliente.AceitaReceberOfertas,
            Ativo = cliente.Ativo,
            DataAnonimizacao = cliente.DataAnonimizacao
        };
    }
}
