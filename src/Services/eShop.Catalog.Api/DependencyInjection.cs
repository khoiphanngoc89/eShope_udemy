using BuildBlock.Logging.Extensions;
using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Services;
using Microsoft.Extensions.Options;
using eShop.Catalog.Api.Options;
using JasperFx;
using JasperFx.OpenTelemetry;
using BuildingBlock.Abstractions.Data;

namespace eShop.Catalog.Api;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddInfrastructure(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile(
                "appsettings.Development.local.json",
                optional: true,
                reloadOnChange: true);
        }

        return builder;
    }
    internal static WebApplication AddCatalogServices(this WebApplicationBuilder builder)
    {
        builder.AddAppLogging();
        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddFluentValidation();
        builder.Services.AddAppCarter();
        builder.Services.AddAppMediatR();
        builder.Services.AddGenericRepository();
        builder.Services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();
        builder.Services.AddScoped<IImportProductsReader, ImportProductsReader>();
        builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ICatalogUnitOfWork>());
        builder.Services.AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.CatalogDb),
                $"{DatabaseOptions.SectionName}:CatalogDb must be configured.")
            .ValidateOnStart();

        builder.Services.AddMarten(serviceProvider =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var options = new StoreOptions();
            options.Connection(databaseOptions.CatalogDb);
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
            options.OpenTelemetry.TrackConnections = TrackLevel.Normal; // Spans for DB commands
            options.OpenTelemetry.TrackEventCounters(); // Optional: Metrics for Event Store
            return options;
        }).UseLightweightSessions();

        builder.Services.AddAppOpenTelemetry(builder.Environment.ApplicationName, tracing =>
        {
            // Tell OpenTelemetry to listen to Marten's internal engine
            tracing.AddSource("Marten");
        });
        return builder.Build();
    }

    internal static WebApplication UsePipline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            // Serves the interactive Swagger UI page
            app.UseSwaggerUI(options =>
            {
                // Tell Swagger UI where to read the generated OpenAPI JSON document
                options.SwaggerEndpoint("/openapi/v1.json", "My API v1");
            });
        }

        app.UseHttpsRedirection();
        app.MapCarter();
        return app;
    }

    internal static WebApplication Logging(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        app.Logger.LogInformation("{ApplicationName} started", app.Environment.ApplicationName));
        app.Lifetime.ApplicationStopping.Register(() =>
            app.Logger.LogInformation("{ApplicationName} is shutting down", app.Environment.ApplicationName));
        return app;
    }
}