using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record CupomDTO
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public TipoDesconto TipoDesconto { get; set; }
    public decimal ValorDesconto { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public DateTime? DataDevolucao { get; set; }
    public int? LimiteUsoGlobal { get; set; }
    public int? LimiteUsoCliente { get; set; }
    public string? Condicao { get; set; }
    public bool Ativo { get; set; }

    public static CupomDTO FromModel(Cupom model)
    {
        return new CupomDTO
        {
            Id = model.Id,
            Codigo = model.Codigo,
            TipoDesconto = model.TipoDesconto,
            ValorDesconto = model.ValorDesconto,
            DataInicio = model.DataInicio,
            DataFim = model.DataFim,
            DataDevolucao = model.DataDevolucao,
            LimiteUsoGlobal = model.LimiteUsoGlobal,
            LimiteUsoCliente = model.LimiteUsoCliente,
            Condicao = model.Condicao,
            Ativo = model.Ativo
        };
    }
}

public record NovoCupomDTO
{
    public int? Id { get; set; }

    [MaxLength(50)]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "O tipo de desconto é obrigatório.")]
    public TipoDesconto? TipoDesconto { get; set; }

    /// <summary>Ignorado no cupom de extensão de prazo.</summary>
    public decimal ValorDesconto { get; set; }

    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }

    /// <summary>Obrigatória no cupom de extensão de prazo.</summary>
    public DateTime? DataDevolucao { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "O limite de uso global deve ser pelo menos 1.")]
    public int? LimiteUsoGlobal { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "O limite de uso por cliente deve ser pelo menos 1.")]
    public int? LimiteUsoCliente { get; set; }

    [MaxLength(500)]
    public string? Condicao { get; set; }

    public bool Ativo { get; set; } = true;
}

public record ValidarCupomDTO
{
    [Required, MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    public int IdCliente { get; set; }

    public int? IdPedido { get; set; }

    [Required]
    public List<ItemCupomValidacaoDTO> Itens { get; set; } = [];
}

public record ItemCupomValidacaoDTO
{
    [Required]
    public int IdJogo { get; set; }

    [Required]
    public int IdPeriodo { get; set; }
}

public record ValidacaoCupomResultadoDTO
{
    public bool Valido { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public decimal ValorDescontoCalculado { get; set; }
    public TipoDesconto? TipoDesconto { get; set; }
    public decimal? ValorDescontoOriginal { get; set; }
    public int? IdCupom { get; set; }
    /// <summary>Preenchida no cupom de extensão de prazo.</summary>
    public DateTime? DataDevolucao { get; set; }
}
