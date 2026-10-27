using BuildingBlock.Extensions;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Tests;

public class AddAppCarterTests
{
    [Fact]
    public void AddAppCarter_WhenCalled_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddAppCarter();

        Assert.Same(services, returned);
    }

    [Fact]
    public async Task MapCarter_WhenTestModuleIsRegistered_RouteIsNotNotFound()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
        builder.WebHost.UseTestServer();
        builder.Services.AddAppCarter();
        await using var app = builder.Build();
        app.MapCarter();
        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/buildingblock-test");

        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
