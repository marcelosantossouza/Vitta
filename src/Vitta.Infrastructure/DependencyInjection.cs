using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Imports;
using Vitta.Infrastructure.Ai;
using Vitta.Infrastructure.Auth;
using Vitta.Infrastructure.Imports;

namespace Vitta.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiServiceOptions>(configuration.GetSection(AiServiceOptions.SectionName));
        services.AddHttpClient<IAiService, HttpAiService>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<AiServiceOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Key.Length >= 32, "Auth:Jwt:Key precisa ter ao menos 32 caracteres.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISpreadsheetReader, SpreadsheetReader>();
        return services;
    }
}
