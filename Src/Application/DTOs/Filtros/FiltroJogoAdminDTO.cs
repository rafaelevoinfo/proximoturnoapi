using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroJogoAdminDTO : FiltroJogoDTO {
    [FromQuery(Name = "id_categoria")]
    public int? IdCategoria { get; set; }
    [FromQuery(Name = "situacao")]
    public SituacaoJogoFiltro? Situacao { get; set; }
}

/// <summary>Situação do jogo na listagem do admin, combinando o status das cópias.</summary>
public enum SituacaoJogoFiltro {
    /// <summary>Ao menos uma cópia disponível: dá para alugar.</summary>
    Disponiveis,
    /// <summary>Nenhuma disponível e ao menos uma alugada, reservada ou em manutenção.</summary>
    Indisponiveis,
    /// <summary>Todas as cópias ativas só para eventos.</summary>
    ApenasEmEventos,
    /// <summary>Todas as cópias desativadas.</summary>
    Desativados,
}
