using System.Net.Mail;
using Vitta.Domain.Common;

namespace Vitta.Domain.Settings;

/// <summary>
/// E-mail diario com o resumo da corretora (follow-ups do dia, saidas da carteira, comissao). Registro unico
/// enquanto a Vitta nao e multi-corretora; quando for, passa a existir um por corretora. Os dados do servidor de
/// e-mail (SMTP) NAO ficam aqui: vivem no ambiente (.env), como as chaves de IA.
/// </summary>
public sealed class NotificationSettings : AggregateRoot
{
    public static readonly Guid SingletonId = Guid.Parse("0199a000-0000-7000-8000-0000000000a2");
    public const int MaxRecipients = 20;
    public static readonly TimeOnly DefaultSendAt = new(7, 0);

    private NotificationSettings() { } // EF

    public bool DailyEmailEnabled { get; private set; }

    /// <summary>Destinatarios separados por ";" (coluna unica, lista curta).</summary>
    public string RecipientList { get; private set; } = string.Empty;

    /// <summary>Horario de Brasilia em que o e-mail diario sai.</summary>
    public TimeOnly SendAt { get; private set; } = DefaultSendAt;

    /// <summary>Ultimo dia (Brasilia) em que o envio agendado aconteceu; evita mandar duas vezes no mesmo dia.</summary>
    public DateOnly? LastSentOn { get; private set; }

    public IReadOnlyList<string> Recipients =>
        RecipientList.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static NotificationSettings CreateDefault() => new() { Id = SingletonId };

    public Result Update(bool dailyEmailEnabled, IEnumerable<string> recipients, TimeOnly sendAt)
    {
        var cleaned = new List<string>();
        foreach (var raw in recipients ?? [])
        {
            var email = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (email.Length == 0)
            {
                continue;
            }

            if (!IsEmail(email))
            {
                return NotificationSettingsErrors.InvalidEmail(raw!.Trim());
            }

            if (!cleaned.Contains(email))
            {
                cleaned.Add(email);
            }
        }

        if (cleaned.Count > MaxRecipients)
        {
            return NotificationSettingsErrors.TooManyRecipients;
        }

        if (dailyEmailEnabled && cleaned.Count == 0)
        {
            return NotificationSettingsErrors.RecipientsRequired;
        }

        DailyEmailEnabled = dailyEmailEnabled;
        RecipientList = string.Join(';', cleaned);
        SendAt = new TimeOnly(sendAt.Hour, sendAt.Minute);
        Touch();
        return Result.Success();
    }

    /// <summary>Ligado, com destinatarios, ja passou do horario e ainda nao saiu hoje.</summary>
    public bool IsDue(DateTime localNow) =>
        DailyEmailEnabled
        && Recipients.Count > 0
        && LastSentOn != DateOnly.FromDateTime(localNow)
        && TimeOnly.FromDateTime(localNow) >= SendAt;

    public void MarkSent(DateOnly day)
    {
        LastSentOn = day;
        Touch();
    }

    private static bool IsEmail(string value) =>
        value.Length <= 200 && MailAddress.TryCreate(value, out var address) && address.Address == value && value.Contains('.');
}

public static class NotificationSettingsErrors
{
    public static readonly Error RecipientsRequired = Error.Validation("Notifications.RecipientsRequired", "Informe ao menos um e-mail para ligar o resumo diário.");
    public static readonly Error TooManyRecipients = Error.Validation("Notifications.TooManyRecipients", $"Use no máximo {NotificationSettings.MaxRecipients} e-mails.");
    public static readonly Error EmailNotConfigured = Error.Validation("Notifications.EmailNotConfigured",
        "O envio de e-mail não está configurado no servidor (Email__Host, Email__Username, Email__Password e Email__From no .env).");
    public static Error InvalidEmail(string value) => Error.Validation("Notifications.InvalidEmail", $"E-mail inválido: {value}");
}
