using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GSB_project.Testing;

// Linked only into test tools. Never referenced by production WebApi.
public static class LocalTestIdentity
{
    public const string Issuer = "https://local-test.invalid";
    public const string Audience = "gsb-local-tests";
    private static readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(32));

    public static void Configure(IServiceCollection services) =>
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.Audience = Audience;
            options.TokenValidationParameters.ValidIssuer = Issuer;
            options.TokenValidationParameters.ValidAudience = Audience;
            options.TokenValidationParameters.IssuerSigningKey = Key;
            options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        });

    public static string Token(string? role = "Manager", string? issuer = null, string? audience = null,
        bool expired = false, bool wrongKey = false)
    {
        var claims = new List<Claim> { new("sub", "local-test-user"), new("jti", Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new("role", role));
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience, claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(expired ? -5 : 15),
            signingCredentials: new SigningCredentials(wrongKey ?
                new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) : Key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
