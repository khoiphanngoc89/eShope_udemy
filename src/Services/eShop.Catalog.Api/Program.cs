

var builder = WebApplication.CreateBuilder(args);


var app = builder.AddCatalogServices().UsePipline();

app.Run();
