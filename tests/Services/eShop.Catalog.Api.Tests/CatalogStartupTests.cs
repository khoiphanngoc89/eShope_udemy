using eShop.Catalog.Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Api.Tests;

public class CatalogStartupTests
{
    [Fact]
    public void CreateClient_WhenCatalogDbIsMissing_FailsNamingConnectionStringsCatalogDb()
    {
        using var factory = new MissingCatalogDbFactory();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:CatalogDb", exception.ToString());
    }

    [Fact]
    public void CreateHost_WhenDevelopmentLocalSettingsExist_LoadsCatalogDb()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"CatalogStartupTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        const string connectionString = "Host=127.0.0.1;Database=catalog_tests;Username=test;Password=test";

        try
        {
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.Development.local.json"),
                $$"""
                {
                  "ConnectionStrings": {
                    "CatalogDb": "{{connectionString}}"
                  }
                }
                """);

            using var factory = new DevelopmentLocalSettingsFactory(contentRoot);

            var options = factory.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            Assert.Equal(connectionString, options.CatalogDb);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private sealed class MissingCatalogDbFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatalogDb"] = ""
                });
            });
        }
    }

    private sealed class DevelopmentLocalSettingsFactory(string contentRoot) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(contentRoot);
        }
    }
}
