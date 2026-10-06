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

    /// <summary>A cópia como o admin a deixou no formulário do jogo. Id 0 é cópia nova.</summary>
    public readonly record struct CopiaDesejada(int Id, StatusJogo Status);

    /// <summary>
    /// Aplica às cópias do jogo o que o admin deixou no formulário: cria as novas, troca o
    /// status manual das existentes e desativa as que ele tirou da lista. Desativar, e não
    /// apagar, porque a cópia pode estar em pedidos antigos. Cópias já desativadas não vêm
    /// no formulário e ficam como estão.
    /// <para>
    /// Valida tudo antes de mexer em qualquer cópia: com erro, devolve as mensagens e nada muda.
    /// </para>
    /// </summary>
    public static List<string> SincronizarCopias(ICollection<JogoCopia> atuais, IReadOnlyList<CopiaDesejada> desejadas) {
        var erros = new List<string>();
        if (desejadas.Count == 0) {
            erros.Add("O jogo precisa de pelo menos uma cópia. Para tirá-lo do catálogo, use Desativar na lista de jogos.");
            return erros;
        }

        static bool DoPedido(StatusJogo s) => s is StatusJogo.Reservado or StatusJogo.Alugado;
        static string Situacao(StatusJogo s) => s == StatusJogo.Reservado ? "reservada num pedido" : "alugada";

        var mudancas = new List<(JogoCopia Copia, StatusJogo Status)>();
        foreach (var desejada in desejadas.Where(d => d.Id != 0)) {
            var copia = atuais.FirstOrDefault(c => c.Id == desejada.Id);
            if (copia is null) {
                erros.Add($"A cópia #{desejada.Id} não pertence a este jogo.");
            } else if (copia.Status != desejada.Status) {
                if (DoPedido(copia.Status)) {
                    erros.Add($"A cópia #{copia.Id} está {Situacao(copia.Status)}: o status dela muda sozinho com o pedido.");
                } else if (!Manuais.Contains(desejada.Status)) {
                    erros.Add($"Status inválido para a cópia #{copia.Id}: use Disponível, Apenas em eventos ou Em manutenção.");
                } else {
                    mudancas.Add((copia, desejada.Status));
                }
            }
        }

        var mantidas = desejadas.Select(d => d.Id).ToHashSet();
        foreach (var copia in atuais.Where(c => c.Status != StatusJogo.Desativado && !mantidas.Contains(c.Id))) {
            if (DoPedido(copia.Status)) {
                erros.Add($"A cópia #{copia.Id} está {Situacao(copia.Status)} e não pode ser excluída.");
            } else {
                mudancas.Add((copia, StatusJogo.Desativado));
            }
        }

        var novas = desejadas.Where(d => d.Id == 0).ToList();
        if (novas.Any(n => !Manuais.Contains(n.Status))) {
            erros.Add("Status inválido para cópia nova: use Disponível, Apenas em eventos ou Em manutenção.");
        }

        if (erros.Count > 0) {
            return erros;
        }

        foreach (var (copia, status) in mudancas) {
            copia.Status = status;
        }
        foreach (var nova in novas) {
            atuais.Add(new JogoCopia { Status = nova.Status });
        }
        return erros;
    }
}
