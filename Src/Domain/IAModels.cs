namespace ProximoTurnoApi.Domain;

public record IAModel {
    //Prices-------------------------------------- $0.065 / $0.26 ------------- $0.75 / $3.75 -------------- $5 / $25 ----
    public static readonly string[] OCR_MODELS = ["qwen/qwen3.5-flash-02-23", "google/gemini-3.6-flash", "anthropic/claude-opus-5"];
}