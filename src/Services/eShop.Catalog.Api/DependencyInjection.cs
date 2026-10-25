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
        return builder.Build();
    }

    public static WebApplication UsePipline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.MapCarter();
        return app;
    }
}