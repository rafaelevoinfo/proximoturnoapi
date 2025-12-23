namespace ProximoTurnoApi.Application.DTOs;

public abstract class StringUtils {
    public static string Capitalize(string input) {
        return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(input);
    }
}