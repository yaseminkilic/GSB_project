using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSB_project.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GSB_project.Tests;

public class ApiTests
{
    private static object Event() => new { Name = "Test etkinligi", StartAt = DateTime.UtcNow.AddDays(2), Capacity = 10 };
    private static object Participant(string email = "test@example.com") => new { FullName = "Test Kullanici", Email = email, Phone = (string?)null };

    private static async Task<int> Create(HttpClient client, string route, object payload)
    {
        using var response = await client.PostAsJsonAsync(route, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.EndsWith($"/{id}", response.Headers.Location.ToString());
        return id;
    }

    [Fact]
    public async Task Complete_registration_flow_persists_and_returns_expected_fields()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();

        var before = DateTime.UtcNow;

        var eventId = await Create(client, "/api/events", Event());
        var participantId = await Create(client, "/api/participants", Participant());
        var id = await Create(client, "/api/registrations", new { EventId = eventId, ParticipantId = participantId });
        
        using var response = await client.GetAsync($"/api/registrations/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(eventId, body.GetProperty("eventId").GetInt32());
        Assert.Equal(participantId, body.GetProperty("participantId").GetInt32());
        Assert.False(body.GetProperty("isCancelled").GetBoolean());
        Assert.InRange(body.GetProperty("registeredAt").GetDateTime(), before, DateTime.UtcNow);
        
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Registrations.CountAsync());

        var eventBody = await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}");
        Assert.Equal("Test etkinligi", eventBody.GetProperty("name").GetString());
        
        var participantBody = await client.GetFromJsonAsync<JsonElement>($"/api/participants/{participantId}");
        Assert.Equal("test@example.com", participantBody.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("events")]
    [InlineData("participants")]
    [InlineData("registrations")]
    public async Task Missing_resource_returns_404(string resource)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.GetAsync($"/api/{resource}/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", "valid@example.com", "FullName")]
    [InlineData("Test", "", "Email")]
    [InlineData("Test", "invalid", "Email")]
    public async Task Invalid_participant_returns_field_errors_without_writing(string name, string email, string field)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.PostAsJsonAsync("/api/participants", new { FullName = name, Email = email });
        await AssertValidation(response, field);
        using var scope = api.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Participants.CountAsync());
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        await Create(client, "/api/participants", Participant());
        using var response = await client.PostAsJsonAsync("/api/participants", Participant());
        await AssertValidation(response, "Email");
        using var scope = api.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Participants.CountAsync());
    }

    [Theory]
    [InlineData(0, 0, "EventId")]
    [InlineData(999, 0, "EventId")]
    [InlineData(0, 999, "ParticipantId")]
    public async Task Unknown_registration_references_are_rejected(int eventId, int participantId, string field)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.PostAsJsonAsync("/api/registrations", new { EventId = eventId, ParticipantId = participantId });
        await AssertValidation(response, field);
        using var scope = api.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Registrations.CountAsync());
    }

    [Fact]
    public async Task Duplicate_registration_is_rejected()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        var eventId = await Create(client, "/api/events", Event());
        var participantId = await Create(client, "/api/participants", Participant());
        var payload = new { EventId = eventId, ParticipantId = participantId };
        await Create(client, "/api/registrations", payload);
        using var response = await client.PostAsJsonAsync("/api/registrations", payload);
        await AssertValidation(response, "");
        using var scope = api.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Registrations.CountAsync());
    }

    [Fact]
    public async Task Invalid_event_is_rejected_by_pipeline_without_writing()
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient();
        using var response = await client.PostAsJsonAsync("/api/events", new { Name = "", StartAt = DateTime.UtcNow.AddDays(-1), Capacity = 0 });
        await AssertValidation(response, "Name");
        using var scope = api.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.CountAsync());
    }

    private static async Task AssertValidation(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("propertyName").GetString() == field &&
            !string.IsNullOrWhiteSpace(error.GetProperty("errorMessage").GetString()));
    }
}
