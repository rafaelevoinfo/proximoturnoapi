using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Flunt.Notifications;

namespace ProximoTurnoApi.Infrastructure.Models;

public abstract class BaseModel : Notifiable<Notification> {
    [Key, Column("ID")]
    public int Id { get; set; }
}