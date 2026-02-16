using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ProximoTurnoApi.Infrastructure.Models;

// [Table("USUARIO")]
public class Usuario : IdentityUser {
    [Column("NOME"), MaxLength(60)]
    public string? Nome { get; set; } = null!;
}

public abstract class Roles {
    public const string Admin = "Admin";
    public const string Member = "Member";
}