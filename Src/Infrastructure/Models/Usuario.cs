using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ProximoTurnoApi.Infrastructure.Models;

// [Table("USUARIO")]
public class Usuario : IdentityUser {
    // [Key, Column("ID")]
    // public int Id { get; set; }

    // [Column("NOME")]
    // public string Nome { get; set; } = null!;

    // [Column("EMAIL")]
    // public string Email { get; set; } = null!;

    // [Column("ADMIN")]
    // public bool IsAdmin { get; set; }
}