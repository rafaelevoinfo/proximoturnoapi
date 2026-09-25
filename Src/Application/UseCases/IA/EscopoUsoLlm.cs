namespace ProximoTurnoApi.Application.UseCases.IA;

/// <summary>O que estava sendo feito quando a chamada de LLM saiu.</summary>
public sealed record AlvoUsoLlm(int? IdJogo, int? IdJogoLink, string? Alvo);

/// <summary>
/// Diz ao ledger a que manual pertence o gasto. Ambiente em vez de parâmetro porque entre
/// quem sabe o alvo (o caso de uso) e quem mede o gasto (a policy do pipeline) existem o
/// extrator, o revisor e o SDK, e nenhum deles tem por que carregar isso.
/// </summary>
public static class EscopoUsoLlm {

    private static readonly AsyncLocal<AlvoUsoLlm?> _atual = new();

    public static AlvoUsoLlm? Atual => _atual.Value;

    /// <summary>Consumir com <c>using</c>: descartar restaura o escopo anterior.</summary>
    public static IDisposable Abrir(int? idJogo, int? idJogoLink, string? alvo) {
        var anterior = _atual.Value;
        _atual.Value = new AlvoUsoLlm(idJogo, idJogoLink, alvo);
        return new Escopo(anterior);
    }

    private sealed class Escopo(AlvoUsoLlm? _anterior) : IDisposable {

        private bool _fechado;

        public void Dispose() {
            // Descartar duas vezes nao pode restaurar um escopo velho por cima do atual.
            if (_fechado) {
                return;
            }

            _fechado = true;
            _atual.Value = _anterior;
        }
    }
}
