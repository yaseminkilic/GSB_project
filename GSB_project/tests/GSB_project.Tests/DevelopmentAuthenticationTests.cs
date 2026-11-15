using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using GSB_project.WebApi.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace GSB_project.Tests;

public class DevelopmentAuthenticationTests
{
    private static ServiceProvider Services(string environment, byte[] key, string? authority = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Audience"] = "gsb-api",
            ["Authentication:Authority"] = authority,
            ["Authentication:Schemes:Bearer:ValidIssuer"] = "dotnet-user-jwts",
            ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = "dotnet-user-jwts",
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(key)
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Mock.Of<IHostEnvironment>(host => host.EnvironmentName == environment));
        services.AddApiSecurity();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Development_accepts_signed_user_jwts_with_expected_audience_and_role()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        using var services = Services("Development", key);
        var options = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var token = handler.WriteToken(new JwtSecurityToken("dotnet-user-jwts", "gsb-api",
            [new Claim("role", "Manager")], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
        var principal = handler.ValidateToken(token, options.TokenValidationParameters, out _);
        Assert.True(principal.IsInRole("Manager"));
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_does_not_accept_local_signing_keys(string environment)
    {
        using var services = Services(environment, RandomNumberGenerator.GetBytes(32));
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer"));
    }

    [Fact]
    public void Configured_authority_takes_precedence_over_local_keys()
    {
        using var services = Services("Development", RandomNumberGenerator.GetBytes(32), "https://trusted.example.test");
        var options = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");
        Assert.Empty(options.TokenValidationParameters.IssuerSigningKeys ?? []);
        Assert.Null(options.TokenValidationParameters.IssuerSigningKey);
        Assert.Equal("https://trusted.example.test", options.Authority);
    }

    [Fact]
    public void Weak_local_signing_key_is_rejected()
    {
        using var services = Services("Development", RandomNumberGenerator.GetBytes(16));
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer"));
    }
}
