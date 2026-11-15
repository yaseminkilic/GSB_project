using GSB_project.Application;
using GSB_project.Infrastructure;
using Scalar.AspNetCore;
using GSB_project.WebApi.Security;
using System.Globalization;
using System.Threading.RateLimiting;

var app = Program.BuildApplication(args);
app.Run();

public partial class Program
{
    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

        builder.Services.AddRateLimiter(options =>
        {
            var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
            var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

            if (permitLimit <= 0 || windowSeconds <= 0)
                throw new InvalidOperationException("Rate limiting values must be positive.");

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                
                await Results.Problem(statusCode: 429, title: "Too many requests.").ExecuteAsync(context.HttpContext);
            };
        });
        
        builder.Services.AddApiSecurity();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
        builder.Services.AddOpenApi();

        configure?.Invoke(builder);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                }
                return Task.CompletedTask;
            });
            await next();
        });
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }
        app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
        {
            var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            if (exception is FluentValidation.ValidationException validationEx)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    Errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
                });
            }
            else
            {
                await Results.Problem(statusCode: 500, title: "An unexpected error occurred.",
                    extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
            }
        }));
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}


