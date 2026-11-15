using GSB_project.Testing;
using System.Net.Http.Headers;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace GSB_project.Tests;

// Each test owns a real relational, isolated database. No production SQL connection is used.
public sealed class TestApi : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private readonly Action<IServiceCollection>? configureServices;
    private readonly int permitLimit;

    public TestApi(int permitLimit = 100, Action<IServiceCollection>? configureServices = null)
    {
        this.permitLimit = permitLimit;
        this.configureServices = configureServices;
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = permitLimit.ToString() }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            LocalTestIdentity.Configure(services);
            configureServices?.Invoke(services);
        });
    }

    public HttpClient CreateTestClient(bool authenticated = true, string role = "Manager")
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        if (authenticated) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalTestIdentity.Token(role));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

