using GymAccess.Data;
using GymAccess.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymAccess.Tests;

/// <summary>
/// Levanta la API real en memoria para los tests HTTP, con una base SQLite en memoria (con sus migraciones aplicadas)
/// y un reloj fijo: lunes 2/11/2026, 15:00 UTC = 12:00 en Buenos Aires.
/// </summary>
public sealed class GymApiFactory : WebApplicationFactory<Program>
{
    // La base en memoria vive mientras esta conexión esté abierta.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public GymApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymDbContext>>();
            services.AddDbContext<GymDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock(TestData.Now));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;

        public DateTime UtcNow { get; }
    }
}
