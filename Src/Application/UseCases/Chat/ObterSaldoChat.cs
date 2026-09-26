using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Crédito do chat, calculado na hora a partir dos aluguéis e do ledger de LLM. Sem tabela
/// de saldo: não tem como divergir do gasto real, e um aluguel novo já renova o crédito.
/// </summary>
public class ObterSaldoChat(IChatRegrasRepository _repositorio,
                            ICotacaoDolar _cotacao,
                            ConfiguracaoChat _configuracao) : UseCaseBasico {

    public async Task<SaldoChatDTO> ExecuteAsync(string idUsuario, string? email, bool admin, CancellationToken cancellationToken = default) {
        var cotacao = await _cotacao.ObterAsync(cancellationToken);
        var valorBase = string.IsNullOrWhiteSpace(email) ? 0m : await _repositorio.SomarValorBaseAlugadoAsync(email);
        var gasto = await _repositorio.SomarGastoAsync(idUsuario);

        return Calcular(valorBase, gasto, cotacao, _configuracao, admin);
    }

    public static SaldoChatDTO Calcular(decimal valorBaseAlugado, decimal gasto, CotacaoUsdBrl cotacao,
                                        ConfiguracaoChat configuracao, bool admin) {
        var creditoAlugueis = Math.Round(valorBaseAlugado * configuracao.PercentualCredito, 4);
        var bonus = Math.Round(configuracao.BonusUsd * cotacao.Valor, 4);

        return new SaldoChatDTO {
            ValorBaseAlugado = valorBaseAlugado,
            CreditoAlugueis = creditoAlugueis,
            Bonus = bonus,
            Gasto = Math.Round(gasto, 4),
            Saldo = Math.Round(creditoAlugueis + bonus - gasto, 4),
            Ilimitado = admin,
            CotacaoUsdBrl = cotacao.Valor,
            FonteCotacao = cotacao.Fonte,
        };
    }

    /// <summary>Admin não tem limite; para os demais, saldo zerado ou negativo bloqueia.</summary>
    public static bool PodePerguntar(SaldoChatDTO saldo) => saldo.Ilimitado || saldo.Saldo > 0;
}
