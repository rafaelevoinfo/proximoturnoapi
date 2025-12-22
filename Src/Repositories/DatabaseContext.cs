using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Repositories;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        ConfigurePedido(modelBuilder);
        ConfigureJogo(modelBuilder);

        modelBuilder.Entity<CategoriaPreco>()
            .HasOne(cp => cp.Categoria)
            .WithMany()
            .HasForeignKey(cp => cp.IdCategoria)
            .OnDelete(DeleteBehavior.Cascade);

    }

    private static void ConfigureJogo(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Jogo>()
                    .HasMany(j => j.Links)
                    .WithOne()
                    .HasForeignKey(l => l.IdJogo)
                    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Jogo>()
            .HasMany(j => j.Tags)
            .WithMany(t => t.Jogos)
            .UsingEntity<Dictionary<string, object>>(
                "JOGO_TAG",
                j => j.HasOne<Tag>().WithMany().HasForeignKey("ID_TAG").OnDelete(DeleteBehavior.Cascade),
                t => t.HasOne<Jogo>().WithMany().HasForeignKey("ID_JOGO").OnDelete(DeleteBehavior.Cascade),
                je => je.HasKey("ID_JOGO", "ID_TAG")
            );
        modelBuilder.Entity<Jogo>()
           .HasOne(j => j.Categoria)
           .WithMany()
           .HasForeignKey(j => j.IdCategoria)
           .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePedido(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Pedido>()
                    .HasMany(p => p.Jogos)
                    .WithOne()
                    .HasForeignKey(pj => pj.IdPedido)
                    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Cliente)
            .WithOne()
            .HasForeignKey<Pedido>(p => p.IdCliente)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PedidoJogo>()
            .HasOne(pj => pj.Jogo)
            .WithMany()
            .HasForeignKey(pj => pj.IdJogo)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Jogo> Jogos { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<CategoriaPreco> CategoriaPrecos { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }

}