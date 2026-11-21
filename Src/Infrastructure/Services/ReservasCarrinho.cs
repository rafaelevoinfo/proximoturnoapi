namespace ProximoTurnoApi.Infrastructure.Services;

/// <summary>
/// Segura uma cópia de jogo para o carrinho de um cliente por alguns minutos, sem gravar nada
/// no banco. A cópia continua Disponível em JOGO_COPIA; quem lê o catálogo desconta as cópias
/// seguradas por outros carrinhos.
/// </summary>
public interface IReservasCarrinho {
    TimeSpan Duracao { get; }

    /// <summary>
    /// Segura uma cópia do jogo para o carrinho. <paramref name="copiasDisponiveis"/> é quantas
    /// cópias estão Disponíveis no banco: só reserva se sobrar alguma não segurada por outro
    /// carrinho. Reservar de novo um jogo que o carrinho já segura não renova o prazo: os 15
    /// minutos contam de quando o jogo entrou no carrinho.
    /// Devolve o fim da reserva, ou null quando não sobrou cópia.
    /// </summary>
    DateTimeOffset? Reservar(Guid idCarrinho, int idJogo, int copiasDisponiveis);

    void Liberar(Guid idCarrinho, int idJogo);
    void LiberarTudo(Guid idCarrinho);

    /// <summary>Cópias do jogo seguradas por carrinhos que não são o informado.</summary>
    int SeguradasPorOutros(int idJogo, Guid? idCarrinho);

    /// <summary>Reservas ainda válidas do carrinho: jogo → fim da reserva.</summary>
    IReadOnlyDictionary<int, DateTimeOffset> DoCarrinho(Guid idCarrinho);
}

/// <summary>
/// Reservas num dicionário em memória. A expiração é preguiçosa: cada reserva guarda quando
/// vence e quem consulta ignora as vencidas, então não há timer nem job. As vencidas são
/// apagadas a cada escrita, o que mantém a memória do tamanho das reservas ativas.
/// <para>
/// Vale para uma instância só da API. Reiniciar a API apaga as reservas, o que só devolve os
/// jogos para Disponível antes da hora: o pedido continua validando a cópia no banco.
/// </para>
/// </summary>
public class ReservasCarrinhoMemoria(TimeProvider relogio) : IReservasCarrinho {
    public static readonly TimeSpan DuracaoPadrao = TimeSpan.FromMinutes(15);

    private readonly object _trava = new();
    // jogo → (carrinho → fim da reserva)
    private readonly Dictionary<int, Dictionary<Guid, DateTimeOffset>> _porJogo = [];

    public TimeSpan Duracao { get; init; } = DuracaoPadrao;

    public DateTimeOffset? Reservar(Guid idCarrinho, int idJogo, int copiasDisponiveis) {
        var agora = relogio.GetUtcNow();
        lock (_trava) {
            RemoverVencidas(agora);
            if (!_porJogo.TryGetValue(idJogo, out var carrinhos)) {
                carrinhos = [];
            }
            if (carrinhos.TryGetValue(idCarrinho, out var atual)) {
                return atual;
            }

            var outros = carrinhos.Keys.Count(c => c != idCarrinho);
            if (outros >= copiasDisponiveis) {
                return null;
            }

            var fim = agora + Duracao;
            carrinhos[idCarrinho] = fim;
            _porJogo[idJogo] = carrinhos;
            return fim;
        }
    }

    public void Liberar(Guid idCarrinho, int idJogo) {
        lock (_trava) {
            if (_porJogo.TryGetValue(idJogo, out var carrinhos) && carrinhos.Remove(idCarrinho) && carrinhos.Count == 0) {
                _porJogo.Remove(idJogo);
            }
            RemoverVencidas(relogio.GetUtcNow());
        }
    }

    public void LiberarTudo(Guid idCarrinho) {
        lock (_trava) {
            foreach (var idJogo in _porJogo.Keys.ToList()) {
                var carrinhos = _porJogo[idJogo];
                if (carrinhos.Remove(idCarrinho) && carrinhos.Count == 0) {
                    _porJogo.Remove(idJogo);
                }
            }
            RemoverVencidas(relogio.GetUtcNow());
        }
    }

    public int SeguradasPorOutros(int idJogo, Guid? idCarrinho) {
        var agora = relogio.GetUtcNow();
        lock (_trava) {
            return _porJogo.TryGetValue(idJogo, out var carrinhos)
                ? carrinhos.Count(c => c.Key != idCarrinho && c.Value > agora)
                : 0;
        }
    }

    public IReadOnlyDictionary<int, DateTimeOffset> DoCarrinho(Guid idCarrinho) {
        var agora = relogio.GetUtcNow();
        lock (_trava) {
            var resultado = new Dictionary<int, DateTimeOffset>();
            foreach (var (idJogo, carrinhos) in _porJogo) {
                if (carrinhos.TryGetValue(idCarrinho, out var fim) && fim > agora) {
                    resultado[idJogo] = fim;
                }
            }
            return resultado;
        }
    }

    private void RemoverVencidas(DateTimeOffset agora) {
        foreach (var idJogo in _porJogo.Keys.ToList()) {
            var carrinhos = _porJogo[idJogo];
            foreach (var idCarrinho in carrinhos.Where(c => c.Value <= agora).Select(c => c.Key).ToList()) {
                carrinhos.Remove(idCarrinho);
            }
            if (carrinhos.Count == 0) {
                _porJogo.Remove(idJogo);
            }
        }
    }
}
