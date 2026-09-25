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
}