namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Regras de crédito do chat de regras. Vêm da configuração (.env) para poderem mudar sem
/// deploy de código.
/// </summary>
/// <param name="PercentualCredito">Fração do valor base de cada aluguel que vira crédito (0,10 = 10%).</param>
/// <param name="BonusUsd">Crédito de boas-vindas de todo usuário, em dólar.</param>
public sealed record ConfiguracaoChat(decimal PercentualCredito, decimal BonusUsd) {

    public static readonly ConfiguracaoChat Padrao = new(0.10m, 0.50m);
}
