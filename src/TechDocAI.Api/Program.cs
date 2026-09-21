using TechDocAI.Api.Endpoints;
using TechDocAI.Infrastructure.Extensions;
using TechDocAI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add API & Infrastructure services
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

// Ensure Database Created only if not testing
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
        dbContext.Database.EnsureCreated();
    }
    catch (Exception)
    {
        // Ignore on startup if backing DB is not yet available
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map Minimal API Endpoints
app.MapDocumentEndpoints();
app.MapIngestionJobEndpoints();

app.Run();

public partial class Program { }
