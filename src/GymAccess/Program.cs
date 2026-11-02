using System.Text.Json.Serialization;
using GymAccess.Configuration;
using GymAccess.Data;
using GymAccess.Rules;
using GymAccess.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<GymOptions>(builder.Configuration.GetSection(GymOptions.SectionName));

builder.Services.AddDbContext<GymDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=gymaccess.db"));

builder.Services.AddSingleton<IClock, UtcClock>();

// Reglas de ingreso: el orden de registro es la prioridad del motivo de rechazo (ver CheckInRules).
foreach (var rule in CheckInRules.CreateDefault())
{
    builder.Services.AddSingleton(rule);
}

builder.Services.AddSingleton<CheckInEvaluator>();
builder.Services.AddScoped<CheckInService>();

var app = builder.Build();

// Sin migraciones: crea el archivo SQLite y las tablas si no existen, así corre con `dotnet run` sin pasos extra.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<GymDbContext>().Database.EnsureCreated();
}

// Falla rápido al arrancar si la zona horaria configurada no existe, en vez de en el primer ingreso.
app.Services.GetRequiredService<CheckInEvaluator>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
