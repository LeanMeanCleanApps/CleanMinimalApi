namespace CleanMinimalApi.Presentation.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Application;
using Application.Versions.Entities;
using CleanMinimalApi.Presentation.Authentication;
using CleanMinimalApi.Presentation.Errors;
using CleanMinimalApi.Presentation.OpenApi;
using CleanMinimalApi.Presentation.Serialization;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

[ExcludeFromCodeCoverage]
public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        #region Logging

        _ = builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var assembly = Assembly.GetEntryAssembly();

            _ = loggerConfiguration.ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
                .Enrich.WithProperty(
                    "AssemblyVersion",
                    assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version)
                .Enrich.WithProperty(
                    "AssemblyInformationalVersion",
                    assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        });

        #endregion Logging

        #region Serialisation

        _ = builder.Services.Configure<JsonOptions>(options =>
            options.SerializerOptions.UseApiDefaults().TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

        #endregion Serialisation

        #region OpenApi

        var authentication = AuthenticationSettings.FromConfiguration(builder.Configuration);

        _ = builder.Services.AddOpenApi(options =>
        {
            _ = options.AddDocumentTransformer<DocumentInfoTransformer>();

            if (authentication.Enabled)
            {
                _ = options.AddDocumentTransformer<BearerSecurityTransformer>();
                _ = options.AddOperationTransformer<BearerSecurityTransformer>();
            }
        });

        #endregion OpenApi

        #region Authentication

        if (authentication.Enabled)
        {
            _ = builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authentication.Authority;
                    options.Audience = authentication.Audience;
                    options.MapInboundClaims = false;
                });
            _ = builder.Services.AddAuthorization();
        }
        else
        {
            // Endpoints always ask for authorization, so without an authority every request is let through.
            _ = builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
        }

        #endregion Authentication

        #region Errors

        _ = builder.Services.AddProblemDetails();
        _ = builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

        #endregion Errors

        #region Project Dependencies

        _ = builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        _ = builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("MovieReviews"));
        _ = builder.Services.AddApplication();
        _ = builder.Services.AddSingleton(VersionInfo.FromAssembly(typeof(WebApplicationBuilderExtensions).Assembly));
        _ = builder.Services.AddHealthChecks().AddInfrastructureHealthChecks("ready");

        #endregion Project Dependencies

        return builder;
    }
}
