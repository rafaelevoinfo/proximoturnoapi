using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Identity;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Application.Converters;
using ProximoTurnoApi.Application.UseCases;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment()) {
    DotNetEnv.Env.Load();
    builder.Configuration.AddEnvironmentVariables();
}

// Add services to the container.
builder.Services.AddDbContext<DatabaseContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString)) {
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
    options.UseMySQL(connectionString);
    options.EnableSensitiveDataLogging();
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IJogoRepository, JogoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IListaDesejosRepository, ListaDesejosRepository>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();
builder.Services.AddScoped<IContratoRepository, ContratoRepository>();
builder.Services.AddSingleton<IContratoQueue, ContratoQueue>();
builder.Services.AddHostedService<ContratoQueueBackgroundService>();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailSender<Usuario>, IdentityEmailSender>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAutentiqueService, AutentiqueService>();
builder.Services.AddScoped<IContratoPdfService, ContratoPdfService>();

// Use Cases
// Wishlist
builder.Services.AddScoped<GerenciarListaDesejos>();

// Pedido
builder.Services.AddScoped<BuscarPedidos>();
builder.Services.AddScoped<CadastroPedido>();
builder.Services.AddScoped<AtualizarPedido>();
builder.Services.AddScoped<AtualizarStatusPedido>();
builder.Services.AddScoped<RenovarPedido>();
builder.Services.AddScoped<DevolverItensPedido>();
builder.Services.AddScoped<ObterRelatorioFaturamento>();

// Comentarios
builder.Services.AddScoped<SalvarComentario>();
builder.Services.AddScoped<PodeComentarJogo>();
builder.Services.AddScoped<ObterComentariosJogo>();
builder.Services.AddScoped<ObterComentariosFiltrados>();
builder.Services.AddScoped<AtualizarStatusComentario>();
builder.Services.AddScoped<ObterComentarioPorId>();
builder.Services.AddScoped<ExcluirComentario>();

// Categoria
builder.Services.AddScoped<CadastroCategoria>();
builder.Services.AddScoped<AtualizarCategoria>();

// Cliente
builder.Services.AddScoped<CadastroCliente>();
builder.Services.AddScoped<AtualizarCliente>();

// Jogo
builder.Services.AddScoped<CadastroJogo>();
builder.Services.AddScoped<AtualizarJogo>();
builder.Services.AddScoped<ObterJogo>();

// Tag
builder.Services.AddScoped<CadastroTag>();
builder.Services.AddScoped<AtualizarTag>();

// Cupom
builder.Services.AddScoped<ValidarCupom>();
builder.Services.AddScoped<CadastroCupom>();
builder.Services.AddScoped<AtualizarCupom>();
builder.Services.AddScoped<ListarCupons>();
builder.Services.AddScoped<ExcluirCupom>();

// Contrato Use Cases
builder.Services.AddScoped<GerarContratoPedido>();
builder.Services.AddScoped<ConsultarContratoPedido>();
builder.Services.AddScoped<ProcessarWebhookAutentique>();


builder.Services.AddIdentityUser();
builder.Services.AddDocumentation();

// Configure CORS based on environment
builder.Services.AddCors(options => {
    if (builder.Environment.IsDevelopment()) {
        // Dev: permitir tudo
        options.AddDefaultPolicy(policy => {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    } else {
        // Prod: permitir apenas proximoturno.com.br
        options.AddDefaultPolicy(policy => {
            policy.WithOrigins("https://proximoturno.com.br")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    }
});

builder.Services.AddHealthChecks();

builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Próximo Turno API v1");
    });
}

app.UseCors();

app.MapHealthChecks("/health");

app.MapGroup("/api")
   .MapIdentityApi<Usuario>();

app.MapControllers();

if (app.Environment.IsDevelopment()) {
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync(Roles.Admin)) {
        await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
    }

    if (!await roleManager.RoleExistsAsync(Roles.Member)) {
        await roleManager.CreateAsync(new IdentityRole(Roles.Member));
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    var admin = await userManager.FindByEmailAsync("contato@proximoturno.com.br");
    if (admin is null) {
        var adminEmail = "contato@proximoturno.com.br";
        admin = new Usuario() {
            Nome = "Rafael",
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        userManager.Options.Password.RequiredLength = 1;
        var result = await userManager.CreateAsync(admin, "1");
        if (!result.Succeeded) {
            throw new Exception("Failed to create admin user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    if (!await userManager.IsInRoleAsync(admin, Roles.Admin)) {
        await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}

app.Run();


static class Extensions {
    public static void AddIdentityUser(this IServiceCollection services) {
        services.AddAuthorization()
                .AddAuthentication()
                //In case you are working with cookies
                //.AddCookie(IdentityConstants.ApplicationScheme);
                .AddBearerToken(IdentityConstants.BearerScheme);


        // services.AddIdentity<Usuario, IdentityRole>(op => {
        //     op.User.RequireUniqueEmail = true;
        // })
        services.AddIdentityCore<Usuario>(options => {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
        })
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
            .AddApiEndpoints()
            .AddEntityFrameworkStores<DatabaseContext>()
            .AddDefaultTokenProviders();
    }

    public static void AddDocumentation(this IServiceCollection services) {
        services.AddOpenApi("v1", options => {
            options.AddDocumentTransformer((document, context, cancellationToken) => {
                document.Info = new OpenApiInfo {
                    Title = "Próximo Turno API",
                    Version = "v1",
                    Description = "API para gerenciamento de jogos de tabuleiro, incluindo funcionalidades de cadastro, consulta e empréstimo de jogos.",
                    Contact = new OpenApiContact {
                        Name = "Rafael",
                        Email = "contato@proximoturno.com.br"
                    }
                };
                var schemeName = "Bearer";
                document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
                if (document.Components.SecuritySchemes == null) {
                    document.Components.SecuritySchemes = new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
                }
                document.Components.SecuritySchemes.Add(schemeName, new Microsoft.OpenApi.OpenApiSecurityScheme {
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Autenticação JWT usando o esquema Bearer. Exemplo: \"Authorization: Bearer {token}\""
                });

                var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement();
                var schemeReference = new Microsoft.OpenApi.OpenApiSecuritySchemeReference(schemeName, document);
                requirement.Add(schemeReference, new List<string>());
                document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
                document.Security.Add(requirement);

                return Task.CompletedTask;
            });
        });
    }
}