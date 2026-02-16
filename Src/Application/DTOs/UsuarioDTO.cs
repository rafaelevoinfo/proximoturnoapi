using System.ComponentModel.DataAnnotations;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

public record UsuarioDTO {
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public bool IsAdmin { get; set; }
}