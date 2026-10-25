using BuildBlock.Logging.Extensions;
using BuildingBlock.Extensions;
using Carter;

namespace eShop.Catalog.Api;

public static class DependencyInjection
{
    public static WebApplication AddCatalogServices(this WebApplicationBuilder builder)
    {
        builder.AddAppLogging();
        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddAppCarter();
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return builder.Build();
    }

    public static WebApplication UsePipline(this WebApplication app)
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
}