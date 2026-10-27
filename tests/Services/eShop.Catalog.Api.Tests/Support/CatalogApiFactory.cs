using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Catalog.Api.Tests.Support;

public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    public InMemoryDocumentSession Documents { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDb"] = "Host=127.0.0.1;Database=catalog_tests;Username=x;Password=x"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentSession>();
            services.AddSingleton(Documents);
            services.AddSingleton<IDocumentSession>(_ => Documents.CreateSession());
        });
    }
}
