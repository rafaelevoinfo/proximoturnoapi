using Flunt.Notifications;

namespace ProximoTurnoApi.Application.UseCases;


public enum UseCaseNotificationType {
    Success,
    Warning,
    Error
}
public class UseCaseNotification : Notification {
    public UseCaseNotificationType Type { get; set; }
    public static UseCaseNotification Create(UseCaseNotificationType type, string message) {
        return new UseCaseNotification {
            Message = message,
            Type = type
        };
    }
}