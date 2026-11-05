namespace CleanMinimalApi.Presentation.Extensions;

using System.Diagnostics.CodeAnalysis;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
#if includeAuth
using Presentation.Authentication;
#endif
using Presentation.Endpoints;
using Scalar.AspNetCore;
using Serilog;

[ExcludeFromCodeCoverage]
public static class WebApplicationExtensions
{
    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        #region Database

        _ = app.Services.InitialiseInfrastructure(app.Configuration.GetValue<bool>("Database:SeedSampleData"));

        #endregion Database

        #region Security

        // HSTS tells browsers to only use HTTPS from then on, so it's left off for local runs.
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("local"))
        {
            _ = app.UseHsts();
        }

        _ = app.UseHttpsRedirection();

        #endregion Security

        #region Logging

        // Before the exception handler, so each request is logged once with the status code the client gets.
        _ = app.UseSerilogRequestLogging();

        #endregion Logging

        #region Errors

        _ = app.UseExceptionHandler();
        _ = app.UseStatusCodePages();

        #endregion Errors

#if includeAuth
        #region Authentication

        if (AuthenticationSettings.FromConfiguration(app.Configuration).Enabled)
        {
            _ = app.UseAuthentication();
        }

        _ = app.UseAuthorization();

        #endregion Authentication

#endif
        #region Health

        // Liveness runs no checks, so it only fails when the process can't answer. Readiness checks the database.
#if includeAuth
        _ = app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        _ = app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
#else
        _ = app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        _ = app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
#endif

        #endregion Health

        #region OpenApi

        _ = app.MapOpenApi();
        _ = app.MapScalarApiReference(options => options
            .WithTitle("CleanMinimalApi API")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .HideDeveloperTools()
            .DisableAgent()
            .DisableMcp()
            .DisableTelemetry());

        #endregion OpenApi

        #region MinimalApi

        _ = app.MapVersionEndpoints();
        _ = app.MapAuthorEndpoints();
        _ = app.MapMovieEndpoints();
        _ = app.MapReviewEndpoints();

        #endregion MinimalApi

        return app;
    }
}
