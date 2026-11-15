using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GSB_project.Testing;

namespace GSB_project.Tests;

public class AuthenticationTests
{
    [Fact]
    public void Missing_trusted_issuer_configuration_prevents_startup()
    {
        using var api = new TestApi(configureServices: services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.TokenValidationParameters.IssuerSigningKey = null;
            }));
        Assert.Throws<OptionsValidationException>(() => api.CreateTestClient());
    }
    [Theory]
    [InlineData("/api/events/1")]
    [InlineData("/api/participants/1")]
    [InlineData("/api/registrations/1")]
    public async Task Anonymous_requests_are_challenged(string path)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(authenticated: false);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("unsigned")]
    public async Task Invalid_tokens_are_rejected_without_error_details(string kind)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(authenticated: false);
        var token = kind switch
        {
            "expired" => LocalTestIdentity.Token(expired: true),
            "issuer" => LocalTestIdentity.Token(issuer: "https://untrusted.invalid"),
            "audience" => LocalTestIdentity.Token(audience: "another-api"),
            "signature" => LocalTestIdentity.Token(wrongKey: true),
            "unsigned" => new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(LocalTestIdentity.Issuer,
                    LocalTestIdentity.Audience, expires: DateTime.UtcNow.AddMinutes(5))),
            _ => "invalid-token"
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/events/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("error_description", string.Join(",", response.Headers.WwwAuthenticate));
    }

    [Theory]
    [InlineData("Reader", 404)]
    [InlineData("Manager", 404)]
    [InlineData("Admin", 404)]
    [InlineData("Unknown", 403)]
    [InlineData(null, 403)]
    public async Task Event_reads_require_an_approved_role(string? role, int expected)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(authenticated: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalTestIdentity.Token(role));
        using var response = await client.GetAsync("/api/events/1");
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/api/participants/1")]
    [InlineData("/api/registrations/1")]
    public async Task Reader_cannot_access_personal_data(string path)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(role: "Reader");
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("participants")]
    [InlineData("registrations")]
    public async Task Reader_cannot_write(string resource)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(role: "Reader");
        using var response = await client.PostAsJsonAsync($"/api/{resource}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Admin")]
    public async Task Management_roles_can_create_events(string role)
    {
        using var api = new TestApi();
        using var client = api.CreateTestClient(role: role);
        using var response = await client.PostAsJsonAsync("/api/events", new
        {
            Name = "Authorized event", StartAt = DateTime.UtcNow.AddDays(1), Capacity = 10
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}


