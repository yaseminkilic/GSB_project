using System.Net;
using System.Text.Json;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using GSB_project.Infrastructure.Persistence;
using GSB_project.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GSB_project.PerformanceHost;

public static class PerformanceEntryPoint
{
    public static async Task Main(string[] args)
    {
        // This executable is a localhost-only test harness, never a production deployment.
        await using var app = global::Program.BuildApplication(args, builder =>
        {
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration["RateLimiting:PermitLimit"] = "100000";
            builder.Services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            builder.Services.RemoveAll<ApplicationDbContext>();
            builder.Services.RemoveAll<IApplicationDbContext>();
            // File-backed SQLite allows concurrent read connections under k6.
            var databasePath = builder.Configuration["LoadTest:DatabasePath"] ?? Path.Combine(Path.GetTempPath(), $"gsb-load-{Guid.NewGuid():N}.db");
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
            builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            LocalTestIdentity.Configure(builder.Services);

        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Events.Add(new Event { Id = 1, Name = "Load test event", StartAt = DateTime.UtcNow.AddDays(1), Capacity = 100 });
            await db.SaveChangesAsync();
        }
        app.MapPost("/local-test/shutdown", () => { app.Lifetime.StopApplication(); return Results.Ok(); }).RequireAuthorization();
        await app.StartAsync();
        var sessionPath = app.Configuration["LoadTest:SessionPath"] ?? "TestResults/local-load-session.json";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sessionPath))!);
        await File.WriteAllTextAsync(sessionPath, JsonSerializer.Serialize(new
        {
            baseUrl = app.Urls.Single(), eventId = 1, token = LocalTestIdentity.Token("Reader")
        }));
        try { await app.WaitForShutdownAsync(); }
        finally { File.Delete(sessionPath); }
    }

}



