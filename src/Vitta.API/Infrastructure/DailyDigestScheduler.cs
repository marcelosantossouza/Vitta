using Mediator;
using Vitta.Application.Features.Notifications;

namespace Vitta.API.Infrastructure;

/// <summary>
/// A cada minuto pergunta se esta na hora do e-mail diario (a regra fica em NotificationSettings.IsDue).
/// Leve de proposito: uma consulta curta por minuto; o trabalho de verdade acontece uma vez por dia.
/// </summary>
internal sealed class DailyDigestScheduler(IServiceScopeFactory scopes, ILogger<DailyDigestScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        var pausedUntil = DateTimeOffset.MinValue;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (DateTimeOffset.UtcNow < pausedUntil)
            {
                continue;
            }

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendDailyDigestCommand(Scheduled: true), stoppingToken);
                if (result.IsFailure)
                {
                    // Ex.: senha do SMTP errada. Nao insiste a cada minuto.
                    logger.LogWarning("Resumo diario nao enviado: {Error}. Nova tentativa em 30 min.", result.Error.Message);
                    pausedUntil = DateTimeOffset.UtcNow + RetryAfterFailure;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Banco fora do ar, etc.: tenta de novo no proximo minuto sem derrubar a API.
                logger.LogError(ex, "Erro no agendador do resumo diario");
                pausedUntil = DateTimeOffset.UtcNow + RetryAfterFailure;
            }
        }
    }
}
