using Mediator;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Vitta.API.Endpoints;
using Vitta.API.Infrastructure;
using Vitta.Application;
using Vitta.Application.Behaviors;
using Vitta.Infrastructure;
using Vitta.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Vitta")
    ?? throw new InvalidOperationException("ConnectionStrings:Vitta nao configurada.");

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPersistence(connectionString);

// Mediator (martinothamar/Mediator, MIT) - dispatcher gerado em tempo de compilacao.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Vitta.Application.AssemblyReference)];
    options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

const string ClientCorsPolicy = "client";
builder.Services.AddCors(o => o.AddPolicy(ClientCorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("Vitta API")); // /scalar

    // Em desenvolvimento aplica as migrations automaticamente.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<VittaDbContext>().Database.MigrateAsync();
}

app.UseCors(ClientCorsPolicy);

app.MapHealthChecks("/health");
var api = app.MapGroup("/api");
api.MapClientEndpoints();
api.MapInsurerEndpoints();

app.Run();

public partial class Program; // para testes de integracao (WebApplicationFactory)
