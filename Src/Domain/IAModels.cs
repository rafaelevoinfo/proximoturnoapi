namespace ProximoTurnoApi.Domain;

public record IAModel {
    //Prices------------------------------------------- $0.75 / $3.75 -------------- $5 / $25 ----
    public static readonly string[] OCR_MODELS = ["google/gemini-3.6-flash", "anthropic/claude-opus-5"];

    // Revisao do markdown extraido: procura erro de leitura do OCR. Chamado uma vez por
    // bloco de ~4000 caracteres, so texto. $0.049/$0.098 por M de tokens: o mesmo preco do
    // llama-3.1-8b que ocupava este lugar e nao acertava o tipo de erro que interessa.
    public const string REVISOR_MODEL = "deepseek/deepseek-v4-flash";

    // Embedding dos chunks do manual. 1536 dimensoes, $0.02/M tokens.
    // Trocar de modelo invalida os vetores ja gravados: eles precisam ser gerados de novo.
    public const string EMBEDDING_MODEL = "openai/text-embedding-3-small";

    // Chat de regras: uma chamada por pergunta, que le os trechos do manual e responde. O
    // mesmo modelo barato do revisor; as regras do que pode ser respondido ficam nas instrucoes.
    public const string CHAT_RESPOSTA_MODEL = "deepseek/deepseek-v4-flash";

    /// <summary>Preço em US$ por milhão de tokens de entrada e de saída.</summary>
    public readonly record struct PrecoPorMilhao(decimal Entrada, decimal Saida);

    /// <summary>
    /// Só para estimar o gasto quando a OpenRouter não devolve <c>usage.cost</c>: custo
    /// nulo não pode sair de graça do crédito do usuário. O valor real, quando vem, manda.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PrecoPorMilhao> PRECOS = new Dictionary<string, PrecoPorMilhao> {
        ["google/gemini-3.6-flash"] = new(0.75m, 3.75m),
        ["anthropic/claude-opus-5"] = new(5m, 25m),
        ["deepseek/deepseek-v4-flash"] = new(0.049m, 0.098m),
        ["openai/text-embedding-3-small"] = new(0.02m, 0m),
    };

    /// <summary>Modelo fora da tabela é estimado pelo mais caro dela: errar para cima.</summary>
    public static readonly PrecoPorMilhao PRECO_DESCONHECIDO = new(5m, 25m);

    public static decimal EstimarCustoUsd(string modelo, int tokensEntrada, int tokensSaida) {
        var preco = PRECOS.TryGetValue(modelo, out var conhecido) ? conhecido : PRECO_DESCONHECIDO;
        return (tokensEntrada * preco.Entrada + tokensSaida * preco.Saida) / 1_000_000m;
    }
}
