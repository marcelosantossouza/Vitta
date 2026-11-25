using System.Globalization;
using Mediator;
using Microsoft.Extensions.Logging;
using Vitta.Application.Abstractions.Notifications;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Common;
using Vitta.Domain.Common;
using Vitta.Domain.Settings;

namespace Vitta.Application.Features.Notifications;

/// <param name="SendAt">"HH:mm", horario de Brasilia.</param>
/// <param name="EmailConfigured">SMTP configurado no servidor; sem ele nada e enviado.</param>
public sealed record NotificationSettingsDto(
    bool DailyEmailEnabled, IReadOnlyList<string> Recipients, string SendAt, DateOnly? LastSentOn, bool EmailConfigured, string? EmailFrom);

// ---------- Consultar ----------

public sealed record GetNotificationSettingsQuery : IQuery<NotificationSettingsDto>;

public sealed class GetNotificationSettingsHandler(INotificationSettingsRepository settings, IEmailSender email)
    : IQueryHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    public async ValueTask<NotificationSettingsDto> Handle(GetNotificationSettingsQuery q, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct) ?? NotificationSettings.CreateDefault();
        return new NotificationSettingsDto(s.DailyEmailEnabled, s.Recipients, s.SendAt.ToString("HH:mm", CultureInfo.InvariantCulture),
            s.LastSentOn, email.IsConfigured, email.From);
    }
}

// ---------- Salvar ----------

public sealed record UpdateNotificationSettingsCommand(bool DailyEmailEnabled, IReadOnlyList<string> Recipients, string SendAt) : ICommand<Result>;

public sealed class UpdateNotificationSettingsHandler(INotificationSettingsRepository settings, IUnitOfWork uow)
    : ICommandHandler<UpdateNotificationSettingsCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateNotificationSettingsCommand c, CancellationToken ct)
    {
        if (!TimeOnly.TryParseExact(c.SendAt ?? string.Empty, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sendAt))
        {
            return Error.Validation("Notifications.InvalidTime", "Horário inválido. Use HH:mm (ex.: 07:00).");
        }

        var current = await settings.GetAsync(ct);
        var target = current ?? NotificationSettings.CreateDefault();
        var result = target.Update(c.DailyEmailEnabled, c.Recipients ?? [], sendAt);
        if (result.IsFailure)
        {
            return result;
        }

        if (current is null)
        {
            settings.Add(target);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Enviar ----------

/// <param name="Scheduled">
/// true = chamado pelo agendador: so envia se estiver na hora e ainda nao saiu hoje (e marca como enviado).
/// false = botao "Enviar agora": envia na hora para a lista salva, sem mexer no agendamento.
/// </param>
public sealed record SendDailyDigestCommand(bool Scheduled) : ICommand<Result<int>>;

public sealed class SendDailyDigestHandler(
    INotificationSettingsRepository settings,
    IDashboardQueries dashboard,
    IPolicyQueries policies,
    IEmailSender email,
    IUnitOfWork uow,
    TimeProvider time,
    ILogger<SendDailyDigestHandler> logger) : ICommandHandler<SendDailyDigestCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(SendDailyDigestCommand c, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var now = time.LocalNow();
        // Agendado: so age na hora certa e com SMTP configurado (sem SMTP a tela ja avisa; nao enche o log).
        if (c.Scheduled && (s is null || !s.IsDue(now) || !email.IsConfigured))
        {
            return 0;
        }

        if (s is null || s.Recipients.Count == 0)
        {
            return NotificationSettingsErrors.RecipientsRequired;
        }

        if (!email.IsConfigured)
        {
            return NotificationSettingsErrors.EmailNotConfigured;
        }

        var today = DateOnly.FromDateTime(now);
        var message = DailyDigest.Build(s.Recipients, await dashboard.GetAsync(today, ct), await policies.SummaryAsync(ct), today, email.AppUrl);
        try
        {
            await email.SendAsync(message, ct);
        }
        catch (EmailSendException ex)
        {
            logger.LogWarning(ex, "Falha ao enviar o resumo diario");
            return Error.Validation("Notifications.SendFailed", $"O servidor de e-mail recusou o envio: {ex.Message}");
        }

        if (c.Scheduled)
        {
            s.MarkSent(today);
            await uow.SaveChangesAsync(ct);
        }

        logger.LogInformation("Resumo diario enviado para {Count} destinatario(s)", s.Recipients.Count);
        return s.Recipients.Count;
    }
}
