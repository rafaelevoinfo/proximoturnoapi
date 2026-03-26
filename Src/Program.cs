using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProximoTurnoApi.Application.Swagger;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment()) {
    DotNetEnv.Env.Load();
    builder.Configuration.AddEnvironmentVariables();
}

// Add services to the container.
builder.Services.AddDbContext<DatabaseContext>(options => {
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(9, 4, 0)));
    options.EnableSensitiveDataLogging();
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddControllers();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IJogoRepository, JogoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();

builder.Services.AddIdentityUser();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
    app.UseSwagger();
    app.UseSwaggerUI();
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
            Email = adminEmail
        };
        userManager.Options.Password.RequiredLength = 1;
        userManager.Options.Password.RequireUppercase = false;
        userManager.Options.Password.RequireNonAlphanumeric = false;
        userManager.Options.Password.RequireLowercase = false;
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
        })
            .AddRoles<IdentityRole>()
            .AddApiEndpoints()
            .AddEntityFrameworkStores<DatabaseContext>();
    }

    public static void AddDocumentation(this IServiceCollection services) {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c => {
            // Define o esquema de segurança (Bearer Token)
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
                Description = "Autenticação JWT usando o esquema Bearer. Exemplo: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            // Adiciona o filtro de operação que aplica o requisito de segurança dinamicamente
            c.OperationFilter<SecurityRequirementsOperationFilter>();
        });
    }
}