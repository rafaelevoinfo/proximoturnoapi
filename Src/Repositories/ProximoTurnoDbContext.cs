using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Data;

public class ProximoTurnoDbContext : DbContext{
    public ProximoTurnoDbContext(DbContextOptions<ProximoTurnoDbContext> options) : base(options){}

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Jogo> Jogos { get; set; }
}