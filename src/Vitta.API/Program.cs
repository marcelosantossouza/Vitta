using System.Text.Json.Serialization;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Vitta.API.Endpoints;
using Vitta.API.Infrastructure;
using Vitta.Application;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Behaviors;
using Vitta.Application.Features.Auth;
using Vitta.Infrastructure;
using Vitta.Infrastructure.Auth;
using Vitta.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPersistence(builder.Configuration, builder.Environment.ContentRootPath);

// Mediator (martinothamar/Mediator, MIT) - dispatcher gerado em tempo de compilacao.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Vitta.Application.AssemblyReference)];
    options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
});

// Enums trafegam como texto ("Active", "Won"...), mais legivel para o front e para quem le a API.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---- Autenticacao (JWT) ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = jwt.Value.SigningKey(),
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));
builder.Services.AddScoped<AdminUserSeeder>();
builder.Services.AddHostedService<Vitta.API.Infrastructure.DailyDigestScheduler>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddHealthChecks();

const string ClientCorsPolicy = "client";
builder.Services.AddCors(o => o.AddPolicy(ClientCorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4300"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("Vitta API")); // /scalar
}

app.UseCors(ClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

await app.Services.MigrateIfConfiguredAsync();
await using (var scope = app.Services.CreateAsyncScope())
{
    var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
    await scope.ServiceProvider.GetRequiredService<AdminUserSeeder>().SeedAsync(seedOptions);
}

app.MapHealthChecks("/health");

var api = app.MapGroup("/api");
api.MapAuthEndpoints(); // login e publico; o restante exige token

var secured = api.MapGroup("").RequireAuthorization();
secured.MapDashboardEndpoints();
secured.MapClientEndpoints();
secured.MapPolicyEndpoints();
secured.MapProspectEndpoints();
secured.MapPortfolioEndpoints();
secured.MapInsurerEndpoints();
secured.MapImportTemplateEndpoints();
secured.MapSettingsEndpoints();

app.Run();

public partial class Program; // para testes de integracao (WebApplicationFactory)
