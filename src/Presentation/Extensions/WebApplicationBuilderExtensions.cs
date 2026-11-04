namespace CleanMinimalApi.Presentation.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Application;
using Application.Common.Telemetry;
using Application.Versions.Entities;
using CleanMinimalApi.Presentation.Authentication;
using CleanMinimalApi.Presentation.Errors;
using CleanMinimalApi.Presentation.OpenApi;
using CleanMinimalApi.Presentation.Serialization;
using CleanMinimalApi.Presentation.Telemetry;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

[ExcludeFromCodeCoverage]
public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        #region Telemetry

        var assembly = typeof(WebApplicationBuilderExtensions).Assembly;
        var telemetry = TelemetrySettings.FromConfiguration(builder.Configuration, builder.Environment.EnvironmentName, assembly);

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(telemetry.ResourceAttributes))
            .WithTracing(tracing => tracing
                .AddSource(ApplicationTelemetry.Name)
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Probes call the health endpoints every few seconds, so tracing them would bury everything else.
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation(options => options.RecordException = true))
            .WithMetrics(metrics => metrics
                .AddMeter(
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.AspNetCore.Authentication",
                    "Microsoft.AspNetCore.Authorization")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased));

        // The exporters read OTEL_EXPORTER_OTLP_ENDPOINT and the other OTEL_* settings themselves.
        if (telemetry.Enabled)
        {
            _ = openTelemetry.UseOtlpExporter();
        }

        #endregion Telemetry

        #region Logging

        _ = builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();

            _ = loggerConfiguration.ReadFrom.Configuration(configuration)
                .Enrich.WithProperty(
                    "AssemblyVersion",
                    assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version)
                .Enrich.WithProperty(
                    "AssemblyInformationalVersion",
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

            // Logs leave over OTLP too, carrying the trace and span IDs of whatever was running when they were written.
            if (telemetry.Enabled)
            {
                _ = loggerConfiguration.WriteTo.OpenTelemetry(
                    options =>
                    {
                        options.ResourceAttributes = telemetry.ResourceAttributes;
                        options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
                    },
                    key => configuration[key]);
            }
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
