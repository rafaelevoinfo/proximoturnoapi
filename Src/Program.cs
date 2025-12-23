using Microsoft.EntityFrameworkCore;
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
});
builder.Services.AddControllers();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IJogoRepository, JogoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IFaixaPrecoRepository, FaixaPrecoRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();