using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record StatusLojaDTO {
    public const string MensagemPadrao = "A loja está fechada no momento. Você pode navegar pelo catálogo, mas novos aluguéis estão temporariamente indisponíveis.";

    public bool LojaFechada { get; set; }

    /// <summary>Aviso a exibir (o do admin ou o padrão). Preenchido só com a loja fechada.</summary>
    public string? Mensagem { get; set; }

    /// <summary>O texto que o admin salvou, sem o padrão: é o que a tela de configuração edita.</summary>
    public string? MensagemPersonalizada { get; set; }

    public static StatusLojaDTO FromModel(ConfiguracaoLoja? model) {
        var personalizada = string.IsNullOrWhiteSpace(model?.MensagemLojaFechada) ? null : model.MensagemLojaFechada;
        var fechada = model?.LojaFechada == true;
        return new StatusLojaDTO {
            LojaFechada = fechada,
            Mensagem = fechada ? personalizada ?? MensagemPadrao : null,
            MensagemPersonalizada = personalizada
        };
    }
}

public record AtualizarStatusLojaDTO {
    [Required]
    public bool LojaFechada { get; set; }

    [MaxLength(500, ErrorMessage = "A mensagem pode ter no máximo 500 caracteres.")]
    public string? Mensagem { get; set; }
}
