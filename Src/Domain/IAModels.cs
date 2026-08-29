namespace ProximoTurnoApi.Domain;

public record IAModel {
    //Prices-------------------------------------- $0.065 / $0.26 ------------- $0.75 / $3.75 -------------- $5 / $25 ----
    public static readonly string[] OCR_MODELS = ["qwen/qwen3.5-flash-02-23", "google/gemini-3.6-flash", "anthropic/claude-opus-5"];

    // Embedding dos chunks do manual. 1536 dimensoes, $0.02/M tokens.
    // Trocar de modelo invalida os vetores ja gravados: eles precisam ser gerados de novo.
    public const string EMBEDDING_MODEL = "openai/text-embedding-3-small";
}