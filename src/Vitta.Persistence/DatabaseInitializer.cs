using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitta.Persistence.Context;

namespace Vitta.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Aplica as migrations do provedor configurado, se Database:MigrateOnStartup = true.</summary>
    public static async Task MigrateIfConfiguredAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<DatabaseOptions>().MigrateOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VittaDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
