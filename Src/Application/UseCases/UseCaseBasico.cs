using Flunt.Notifications;

namespace ProximoTurnoApi.Application.UseCases;

public class UseCaseBasico : Notifiable<UseCaseNotification> {

    public string AggregateErrors() {
        return Notifications.Select(n => n.Message).Aggregate((a, b) => a + ", " + b);
    }
}