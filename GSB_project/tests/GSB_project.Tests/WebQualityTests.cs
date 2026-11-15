using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Application.Participants.Commands.CreateParticipant;
using GSB_project.Application.Registrations.Queries.GetEventById;
using GSB_project.Domain.Entities;
using GSB_project.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit.Abstractions;

namespace GSB_project.Tests;

public class WebQualityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("/api/participants/123", 404)]
    [InlineData("/api/events/123", 404)]
    public async Task Api_responses_include_security_and_no_store_headers(string path, int status)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Unexpected_error_returns_generic_problem_without_internal_details()
    {
        var db = new Mock<IApplicationDbContext>();
        db.SetupGet(x => x.Events).Throws(new InvalidOperationException("SECRET connection password stack details"));
        using var api = new TestApi(configureServices: services =>
        {
            services.RemoveAll<IApplicationDbContext>();
            services.AddScoped(_ => db.Object);
        });

        using var client = api.CreateTestClient();

        using var response = await client.GetAsync("/api/events/1");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET", text);
        Assert.DoesNotContain(nameof(InvalidOperationException), text);

        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Excess_requests_return_429_and_forwarded_header_cannot_bypass_limit()
    {
        using var api = new TestApi(permitLimit: 2);
        using var client = api.CreateTestClient();
        for (var i = 0; i < 2; i++)
        {
            using var accepted = await client.GetAsync("/api/events/1");
            Assert.Equal(HttpStatusCode.NotFound, accepted.StatusCode);
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events/1");
        request.Headers.Add("X-Forwarded-For", "203.0.113.99");
        using var rejected = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.True(rejected.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Https_response_enables_hsts_outside_development()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.GetAsync("https://api.example.test/api/events/1");
        Assert.Contains("max-age=", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Api_documentation_is_not_exposed_outside_development(string path)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_400_without_inserting_data()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var content = new StringContent("{broken", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/participants", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = api.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Participants.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("oversized")]
    public async Task Invalid_email_skips_database_lookup(string email)
    {
        if (email == "oversized") email = new string('a', 150) + "@example.com";
        var db = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var result = await new CreateParticipantCommandValidator(db.Object)
            .ValidateAsync(new CreateParticipantCommand("Ada", email, null));
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        db.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public async Task Phone_length_matches_database_constraint(int length, bool valid)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.PostAsJsonAsync("/api/participants", new
        {
            FullName = "Ada", Email = "ada@example.com", Phone = new string('1', length)
        });
        Assert.Equal(valid ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Read_query_does_not_track_entities()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = new Event { Name = "Read test", StartAt = DateTime.UtcNow.AddDays(1), Capacity = 1 };
        db.Events.Add(entity);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await new GetEventByIdQueryHandler(db).Handle(new(entity.Id), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    [Trait("Category", "PerformanceSmoke")]
    public async Task Warm_read_requests_report_latency_without_machine_dependent_threshold()
    {
        using var api = new TestApi(permitLimit: 1000);
        using var client = api.CreateTestClient();
        using var created = await client.PostAsJsonAsync("/api/events", new
        {
            Name = "Performance", StartAt = DateTime.UtcNow.AddDays(1), Capacity = 10
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var location = created.Headers.Location!;
        using var warmup = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
        
        var durations = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var timer = Stopwatch.StartNew();
            using var response = await client.GetAsync(location);
            timer.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            durations.Add(timer.Elapsed.TotalMilliseconds);
        }
        durations.Sort();
        output.WriteLine($"In-process SQLite, 50 sequential warm GETs: p50={durations[24]:F2}ms p95={durations[47]:F2}ms max={durations[^1]:F2}ms");
    }
}
