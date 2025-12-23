namespace ProximoTurnoApi.Application.DTOs;

public class ApiResultDTO<T> {
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResultDTO<T> CreateSuccessResult(T? data, string? message = null) {
        return new ApiResultDTO<T> {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResultDTO<T> CreateFailureResult(string message) {
        return new ApiResultDTO<T> {
            Success = false,
            Message = message,
            Data = default
        };
    }
}