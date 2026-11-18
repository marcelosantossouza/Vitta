using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Portfolio;
using Vitta.Infrastructure.Ai;
using Vitta.Infrastructure.Portfolio;

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

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPortfolioSpreadsheetReader, MagPortfolioSpreadsheetReader>();
        return services;
    }
}
