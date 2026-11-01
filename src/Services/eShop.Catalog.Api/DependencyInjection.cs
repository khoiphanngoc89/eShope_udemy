using BuildBlock.Logging.Extensions;
using BuildingBlock.Extensions;
using BuildingBlock.Search;
using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Logging;
using eShop.Catalog.Api.Search;
using eShop.Catalog.Api.Services;
using Microsoft.Extensions.Options;
using eShop.Catalog.Api.Options;
using JasperFx;
using JasperFx.OpenTelemetry;
using BuildingBlock.Abstractions.Data;
using OpenTelemetry.Metrics;

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
        builder.AddCatalogErrorLogSink();
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
        builder.Services.AddSingleton(CatalogSearchMetrics.Instance);
        builder.Services.AddSearchIndex<Product>(builder.Configuration, "catalog-products");
        builder.Services.PostConfigure<KafkaOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.GroupId))
            {
                options.GroupId = "eshop.catalog.search";
            }

            if (string.IsNullOrWhiteSpace(options.Topic))
            {
                options.Topic = "eshop.catalog.sync.public.mt_doc_product";
            }
        });

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

        builder.Services.AddAppOpenTelemetry(
            builder.Environment.ApplicationName,
            tracing => tracing.AddSource("Marten"),
            metrics =>
            {
                metrics.AddMeter(CatalogSearchMetrics.MeterName);
                metrics.AddMeter(SearchMetrics.SharedMeterName);
            });
        return builder.Build();
    }

    internal static WebApplication UsePipline(this WebApplication app)
    {
        if (!string.IsNullOrWhiteSpace(app.Configuration[$"{KafkaOptions.SectionName}:BootstrapServers"]))
        {
            app.Services.GetRequiredService<Marten.IDocumentStore>()
                .Storage.ApplyAllConfiguredChangesToDatabaseAsync()
                .GetAwaiter()
                .GetResult();
        }

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