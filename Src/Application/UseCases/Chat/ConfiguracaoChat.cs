namespace ProximoTurnoApi.Application.UseCases.Chat;

/// <summary>
/// Regras de crédito do chat de regras. Vêm da configuração (.env) para poderem mudar sem
/// deploy de código.
/// </summary>
/// <param name="PercentualCredito">Fração do valor base de cada aluguel que vira crédito (0,10 = 10%).</param>
/// <param name="BonusUsd">Crédito de boas-vindas de todo usuário, em dólar.</param>
/// <param name="ScoreMinimo">
/// Similaridade (cosseno) mínima para um trecho do manual ir para o modelo. Abaixo disso o
/// trecho só custaria tokens e poderia confundir a resposta. Os trechos descartados ficam
/// gravados em CHAT_MENSAGEM com o score, para calibrar este corte com perguntas reais.
/// </param>
public sealed record ConfiguracaoChat(decimal PercentualCredito, decimal BonusUsd, float ScoreMinimo = ConfiguracaoChat.ScoreMinimoPadrao) {

    public const float ScoreMinimoPadrao = 0.30f;

    public static readonly ConfiguracaoChat Padrao = new(0.10m, 0.50m);
}
