using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
}
