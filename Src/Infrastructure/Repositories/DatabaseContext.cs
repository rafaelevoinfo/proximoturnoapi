using Flunt.Notifications;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
namespace ProximoTurnoApi.Infrastructure.Repositories;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : IdentityDbContext<Usuario>(options) {

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<Notifiable<Notification>>();

        ConfigurePedido(modelBuilder);
        ConfigureJogo(modelBuilder);

        modelBuilder.Entity<Cliente>(b => {
            b.HasIndex(c => c.Email).IsUnique();
            b.HasIndex(c => c.Telefone).IsUnique();
        });

        modelBuilder.Entity<Categoria>(b => {
            b.HasIndex(c => c.Descricao).IsUnique();
            b.HasMany(c => c.Periodos)
             .WithOne(cp => cp.Categoria)
             .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureCategoriaPeriodo(modelBuilder);

        modelBuilder.Entity<Tag>(b => {
            b.HasIndex(t => t.Nome).IsUnique();
        });

        // Configuração de Conversores para .NET 10 / MySql.EntityFrameworkCore
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()) {
            var properties = entityType.GetProperties();
            foreach (var property in properties) {
                if (property.ClrType == typeof(TimeOnly) || property.ClrType == typeof(TimeOnly?)) {
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TimeOnly, TimeSpan>(
                        timeOnly => timeOnly.ToTimeSpan(),
                        timeSpan => TimeOnly.FromTimeSpan(timeSpan)
                    ));
                }
                else if (property.ClrType == typeof(DateOnly) || property.ClrType == typeof(DateOnly?)) {
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, DateTime>(
                        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                        dateTime => DateOnly.FromDateTime(dateTime)
                    ));
                }
            }
        }
    }

    private static void ConfigureCategoriaPeriodo(ModelBuilder modelBuilder) {
        modelBuilder.Entity<CategoriaPeriodo>(builder => {
            builder.ToTable("CATEGORIA_PERIODO");
            builder.HasKey(cp => cp.Id);
            builder.Property(p => p.Id).HasColumnName("ID");
            builder.Property(p => p.QuantidadeDias).HasColumnName("QTDE_DIAS");
            builder.Property(p => p.Valor).HasColumnName("VALOR").HasPrecision(18, 2);
            builder.HasOne(cp => cp.Categoria)
                   .WithMany(p => p.Periodos)
                   .HasForeignKey("ID_CATEGORIA")
                   .OnDelete(DeleteBehavior.Cascade);
        });
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

        modelBuilder.Entity<Jogo>()
            .HasMany(j => j.Copias)
            .WithOne(c => c.Jogo)
            .HasForeignKey(c => c.IdJogo)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePedido(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Pedido>(builder => {
            builder.ToTable("PEDIDO");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("ID");
            builder.Property(p => p.Status).HasColumnName("STATUS").HasConversion<short>();
            builder.Property(p => p.DataHora).HasColumnName("DATA_HORA");
            builder.Property(p => p.DataHoraEntrega).HasColumnName("DATA_HORA_ENTREGA").IsRequired(false);
            builder.Property(p => p.ValorTotal).HasColumnName("VALOR_TOTAL").HasPrecision(18, 2);

            builder.HasOne(p => p.Cliente)
                   .WithMany()
                   .HasForeignKey("ID_CLIENTE")
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.PedidoOriginal)
                   .WithMany()
                   .HasForeignKey("ID_PEDIDO_ORIGINAL")
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.Items)
                   .WithOne()
                   .HasForeignKey(i => i.IdPedido)
                   .OnDelete(DeleteBehavior.Cascade);

            // Diz explicitamente que o EF deve usar o field
            builder.Navigation(p => p.Items)
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemPedido>()
            .HasOne(pj => pj.JogoCopia)
            .WithMany()
            .HasForeignKey(pj => pj.IdJogoCopia)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Jogo> Jogos { get; set; }
    public DbSet<JogoCopia> JogoCopias { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ItemPedido> ItemPedidos { get; set; }

}