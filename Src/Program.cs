using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.DataProtection;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Identity;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Application.Converters;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Backup;
using ProximoTurnoApi.Infrastructure.Logging;
using Serilog;
using Serilog.Events;
using ProximoTurnoApi.Infrastructure.RAG;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Application.Workers;


using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using ProximoTurnoApi.Domain;
using Qdrant.Client;
using Qdrant.Client.Grpc;

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
builder.Services.AddSingleton<ICategoriaPeriodoCache, CategoriaPeriodoCache>();
builder.Services.AddHostedService<CategoriaPeriodoCacheWarmup>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IListaDesejosRepository, ListaDesejosRepository>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();
builder.Services.AddScoped<IContratoRepository, ContratoRepository>();
builder.Services.AddSingleton<IContratoQueue, ContratoQueue>();
builder.Services.AddHostedService<ContratoQueueBackgroundService>();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddSingleton<IEmailService, EmailService>();

// Backup automatizado
builder.Services.AddSingleton(sp =>
    BackupOptions.DaConfiguracao(
        sp.GetRequiredService<IConfiguration>(),
        builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IEstadoBackupStore>(sp =>
    new EstadoBackupArquivo(sp.GetRequiredService<BackupOptions>().CaminhoEstado));
builder.Services.AddSingleton<IArmazenamentoBackup, ArmazenamentoB2>();
builder.Services.AddScoped<IDumpBanco, DumpBancoMySql>();
builder.Services.AddScoped<ISincronizadorUploads, SincronizadorUploads>();
builder.Services.AddScoped<ExecutarBackup>();
builder.Services.AddHostedService<BackupBackgroundService>();

// Indexacao de manuais (RAG)
builder.Services.AddSingleton<IManualQueue, ManualQueue>();
builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IChunkingExtractor, ChunkingExtractor>();
builder.Services.AddScoped<IEmbeddingExtractor, EmbeddingExtractor>();
builder.Services.AddScoped<IManualVectorStore, QdrantManualVectorStore>();

// Singleton: o QdrantClient carrega o canal gRPC e nao pode ser criado por requisicao.
builder.Services.AddSingleton(_ => {
    var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
    var qdrantApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY");
    if (string.IsNullOrWhiteSpace(qdrantUrl) || string.IsNullOrWhiteSpace(qdrantApiKey)) {
        throw new InvalidOperationException("QDRANT_URL ou QDRANT_API_KEY não configuradas.");
    }

    var channel = QdrantChannel.ForAddress(
        qdrantUrl,
        new ClientConfiguration {
            ApiKey = qdrantApiKey
        });

    return new QdrantClient(new QdrantGrpcClient(channel));
});

// Cliente de embedding do OpenRouter. Singleton porque o OpenAIClient e thread-safe e
// segura o pool de conexoes; falhar aqui por falta de chave so derruba a indexacao,
// que roda em background e ja trata o erro por manual.
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => {
    var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    if (string.IsNullOrWhiteSpace(openRouterApiKey)) {
        throw new InvalidOperationException("OPENROUTER_API_KEY não configurada.");
    }

    var openAiClient = new OpenAIClient(new ApiKeyCredential(openRouterApiKey), new OpenAIClientOptions() {
        Endpoint = new Uri("https://openrouter.ai/api/v1"),

        // Embedding responde em segundos, ao contrario da transcricao do PDF inteiro.
        NetworkTimeout = TimeSpan.FromMinutes(2),

        // Chamada curta e idempotente: repetir e barato e resolve falha transitoria.
        RetryPolicy = new ClientRetryPolicy(maxRetries: 2),
    });

    return openAiClient.GetEmbeddingClient(IAModel.EMBEDDING_MODEL).AsIEmbeddingGenerator();
});

builder.Services.AddHostedService<IndexacaoManuaisWorker>();

builder.Services.AddTransient<IEmailSender<Usuario>, IdentityEmailSender>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAutentiqueService, AutentiqueService>();
builder.Services.AddScoped<IContratoPdfService, ContratoPdfService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IResetSenhaLinkService, ResetSenhaLinkService>();

// Use Cases
// Wishlist
builder.Services.AddScoped<GerenciarListaDesejos>();

// Pedido
builder.Services.AddScoped<BuscarPedidos>();
builder.Services.AddScoped<CadastroPedido>();
builder.Services.AddScoped<AtualizarPedido>();
builder.Services.AddScoped<EntregarPedido>();
builder.Services.AddScoped<CancelarPedido>();
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
builder.Services.AddScoped<AtualizarStatusCliente>();
builder.Services.AddScoped<EnviarEmailsClientes>();

// Upload
builder.Services.AddScoped<UploadManual>();

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

builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(hostingContext.Configuration)
    // Fica no codigo, e nao no appsettings, porque o enricher e uma classe deste
    // assembly: via configuracao exigiria expor um metodo de extensao so para isso.
    // O nivel e o corte entre "so a classe" (barato) e "classe + metodo" (pilha).
    .Enrich.With(new CallerEnricher(LogEventLevel.Warning)));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Próximo Turno API v1");
    });
}

app.UseCors();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath)) {
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapHealthChecks("/health");

app.MapGroup("/api")
   .MapIdentityApi<Usuario>();

app.MapControllers();


// A carga inicial roda fora de qualquer requisicao, entao nao existe Activity e os
// logs sairiam sem trace id. O bloco fecha a Activity antes do app.Run(): deixa-la
// aberta faria toda requisicao herdar este mesmo trace id como pai.
using (RastreioBackground.Iniciar("Inicializacao")) {
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
        var result = await userManager.CreateAsync(admin, "admin");
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
        services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "keys")));

        services.AddAuthorization()
                .AddAuthentication()
                //In case you are working with cookies
                //.AddCookie(IdentityConstants.ApplicationScheme);
                .AddBearerToken(IdentityConstants.BearerScheme, options => {
                    options.BearerTokenExpiration = TimeSpan.FromHours(1);
                    options.RefreshTokenExpiration = TimeSpan.FromDays(14);
                });


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