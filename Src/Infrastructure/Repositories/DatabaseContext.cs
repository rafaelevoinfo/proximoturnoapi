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
        ConfigureListaDesejos(modelBuilder);
        ConfigureComentario(modelBuilder);
        ConfigureCupom(modelBuilder);
        ConfigureContratoAutentique(modelBuilder);

        modelBuilder.Entity<Cliente>(b => {
            b.HasIndex(c => c.Email).IsUnique();
            b.HasIndex(c => c.Telefone).IsUnique();
            // No MySQL um índice único ignora as linhas com NULL, então clientes sem CPF
            // (todos os antigos e os do cadastro público) não colidem entre si.
            b.HasIndex(c => c.Cpf).IsUnique();
            b.Property(c => c.Ativo).HasDefaultValue(true);
        });

        modelBuilder.Entity<Categoria>(b => {
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
                } else if (property.ClrType == typeof(DateOnly) || property.ClrType == typeof(DateOnly?)) {
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
                    .HasMany(j => j.Fotos)
                    .WithOne()
                    .HasForeignKey(f => f.IdJogo)
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
            builder.Property(p => p.MetodoPagamento).HasColumnName("METODO_PAGAMENTO").HasMaxLength(50).IsRequired(false);
            builder.Property(p => p.MetodoEntrega).HasColumnName("METODO_ENTREGA").HasMaxLength(50).IsRequired(false);
            builder.Property(p => p.DataHoraAlteracao).HasColumnName("DATA_HORA_ALTERACAO").IsRequired(false);
            builder.Property(p => p.IdCupom).HasColumnName("ID_CUPOM").IsRequired(false);
            builder.Property(p => p.ValorDesconto).HasColumnName("VALOR_DESCONTO").HasPrecision(18, 2);

            builder.HasOne(p => p.Cliente)
                   .WithMany()
                   .HasForeignKey("ID_CLIENTE")
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(p => p.IdPedidoOriginal).HasColumnName("ID_PEDIDO_ORIGINAL").IsRequired(false);

            builder.HasOne(p => p.PedidoOriginal)
                   .WithMany()
                   .HasForeignKey(p => p.IdPedidoOriginal)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Cupom)
                   .WithMany()
                   .HasForeignKey(p => p.IdCupom)
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

        modelBuilder.Entity<ItemPedido>()
            .Property(i => i.Status).HasConversion<short>();
    }

    private static void ConfigureListaDesejos(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ItemListaDesejos>(builder => {
            builder.ToTable("LISTA_DESEJOS");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).HasColumnName("ID");
            builder.HasOne(i => i.Cliente).WithMany().HasForeignKey(i => i.IdCliente).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(i => i.Jogo).WithMany().HasForeignKey(i => i.IdJogo).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(i => new { i.IdCliente, i.IdJogo }).IsUnique(); // Prevent duplicate items per client
        });
    }

    private static void ConfigureComentario(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Comentario>(builder => {
            builder.ToTable("COMENTARIO");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("ID");
            builder.Property(c => c.IdJogo).HasColumnName("ID_JOGO");
            builder.Property(c => c.IdCliente).HasColumnName("ID_CLIENTE");
            builder.Property(c => c.Texto).HasColumnName("TEXTO").HasMaxLength(1000);
            builder.Property(c => c.Nota).HasColumnName("NOTA").HasColumnType("smallint");
            builder.Property(c => c.DataHora).HasColumnName("DATA_HORA");
            builder.Property(c => c.Status).HasColumnName("STATUS").HasDefaultValue(StatusComentario.Pendente);

            builder.HasOne(c => c.Jogo)
                   .WithMany()
                   .HasForeignKey(c => c.IdJogo)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.Cliente)
                   .WithMany()
                   .HasForeignKey(c => c.IdCliente)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => new { c.IdCliente, c.IdJogo }).IsUnique();
        });
    }

    private static void ConfigureCupom(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Cupom>(entity => {
            entity.ToTable("CUPOM");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("ID");
            entity.Property(c => c.Codigo).HasColumnName("CODIGO").HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.Codigo).IsUnique();
            entity.Property(c => c.TipoDesconto).HasColumnName("TIPO_DESCONTO").HasConversion<short>().IsRequired();
            entity.Property(c => c.ValorDesconto).HasColumnName("VALOR_DESCONTO").HasPrecision(18, 2).IsRequired();
            entity.Property(c => c.DataInicio).HasColumnName("DATA_INICIO");
            entity.Property(c => c.DataFim).HasColumnName("DATA_FIM");
            entity.Property(c => c.LimiteUsoGlobal).HasColumnName("LIMITE_USO_GLOBAL");
            entity.Property(c => c.LimiteUsoCliente).HasColumnName("LIMITE_USO_CLIENTE");
            entity.Property(c => c.Condicao).HasColumnName("CONDICAO").HasMaxLength(500);
            entity.Property(c => c.Ativo).HasColumnName("ATIVO").IsRequired();
        });
    }

    private static void ConfigureContratoAutentique(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ContratoAutentique>(builder => {
            builder.ToTable("CONTRATO_AUTENTIQUE");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("ID");
            builder.Property(c => c.Status).HasColumnName("STATUS").HasConversion<short>();
            builder.Property(c => c.Ativo).HasColumnName("ATIVO").HasDefaultValue(true);

            builder.HasOne(c => c.Pedido)
                   .WithMany()
                   .HasForeignKey(c => c.IdPedido)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => c.IdPedido);
            builder.HasIndex(c => c.AutentiqueDocumentId).IsUnique();
        });
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Jogo> Jogos { get; set; }
    public DbSet<JogoCopia> JogoCopias { get; set; }
    public DbSet<JogoLink> JogoLinks { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ItemPedido> ItemPedidos { get; set; }
    public DbSet<ItemListaDesejos> ItensListaDesejos { get; set; }
    public DbSet<Comentario> Comentarios { get; set; }
    public DbSet<Cupom> Cupons { get; set; }
    public DbSet<ContratoAutentique> ContratosAutentique { get; set; }
}