using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace GSB_project.WebApi.Security;

public static class ApiSecurity
{
    public const string ReadEvents = "ReadEvents";
    public const string Manage = "Manage";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"];
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = configuration["Authentication:RoleClaimType"] ?? "role"
                };

                // The SDK stores user-jwts signing keys in user secrets, outside source control.
                // Never trust these local keys outside Development or alongside an OIDC authority.
                if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.Authority))
                {
                    var local = configuration.GetSection("Authentication:Schemes:Bearer");
                    if (local["ValidIssuer"] == "dotnet-user-jwts")
                    {
                        options.TokenValidationParameters.ValidIssuer = "dotnet-user-jwts";
                        options.TokenValidationParameters.ValidAudience = options.Audience;
                        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
                        options.TokenValidationParameters.IssuerSigningKeys = local.GetSection("SigningKeys")
                            .GetChildren()
                            .Where(key => key["Issuer"] == "dotnet-user-jwts")
                            .Select(key => new SymmetricSecurityKey(Convert.FromBase64String(key["Value"] ?? "")))
                            .Where(key => key.KeySize >= 256)
                            .ToArray();
                    }
                }
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Authentication:Audience is required.")
            .Validate(options =>
                (Uri.TryCreate(options.Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https") ||
                options.TokenValidationParameters.IssuerSigningKey is not null ||
                options.TokenValidationParameters.IssuerSigningKeys?.Any() == true,
                "Configure Authentication:Authority with a trusted HTTPS OIDC issuer. " +
                "For local Development, create a token with dotnet user-jwts create --audience gsb-api --role Manager.")
            .ValidateOnStart();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(ReadEvents, policy => policy.RequireAuthenticatedUser().RequireRole("Reader", "Manager", "Admin"));
            options.AddPolicy(Manage, policy => policy.RequireAuthenticatedUser().RequireRole("Manager", "Admin"));
        });
        return services;
    }
}

