using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public enum NotificationChannel
{
    Email,
    WhatsApp
}

public interface INotificationService
{
    Task EnviarNotificacaoNovoPedidoAsync(Pedido pedido);
}
