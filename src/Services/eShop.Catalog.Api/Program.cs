
var builder = WebApplication.CreateBuilder(args)
    .AddInfrastructure();

var app = builder.AddCatalogServices().UsePipline().Logging();

app.Run();