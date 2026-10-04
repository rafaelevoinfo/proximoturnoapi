using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Domain;

/// <summary>Regras de status que envolvem as cópias de um jogo.</summary>
public static class StatusDoJogo {

    /// <summary>
    /// Do mais para o menos alugável: o jogo mostra o status da sua melhor cópia. Uma cópia
    /// alugada volta logo, então vence uma em evento; em evento vence manutenção.
    /// </summary>
    private static readonly StatusJogo[] Prioridade = [
        StatusJogo.Disponivel,
        StatusJogo.Reservado,
        StatusJogo.Alugado,
        StatusJogo.ApenasEmEventos,
        StatusJogo.Manutencao,
    ];

    /// <summary>Status que o admin pode atribuir à mão a uma cópia, e de onde pode tirá-la.</summary>
    public static readonly IReadOnlySet<StatusJogo> Manuais =
        new HashSet<StatusJogo> { StatusJogo.Disponivel, StatusJogo.ApenasEmEventos, StatusJogo.Manutencao };

    /// <summary>
    /// Status do jogo a partir das cópias. Null quando não há cópias carregadas, para quem
    /// chama manter o próprio padrão; Desativado quando todas estão desativadas.
    /// </summary>
    public static StatusJogo? Calcular(IEnumerable<JogoCopia>? copias) {
        var lista = copias?.ToList();
        if (lista is null || lista.Count == 0) {
            return null;
        }

        foreach (var status in Prioridade) {
            if (lista.Any(c => c.Status == status)) {
                return status;
            }
        }
        return StatusJogo.Desativado;
    }
}
