using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;
using Vitta.Application.Abstractions.Notifications;

namespace Vitta.Infrastructure.Notifications;

/// <summary>Configuracao "Email" (no .env de producao: Email__Host, Email__Username...).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Ex.: smtp.hostinger.com</summary>
    public string? Host { get; init; }

    /// <summary>587 com STARTTLS (o SmtpClient do .NET nao suporta SSL implicito na 465).</summary>
    public int Port { get; init; } = 587;

    public string? Username { get; init; }
    public string? Password { get; init; }

    /// <summary>Remetente; normalmente o mesmo e-mail do usuario.</summary>
    public string? From { get; init; }

    public string FromName { get; init; } = "Vitta";
    public bool EnableSsl { get; init; } = true;

    /// <summary>Endereco publico da Vitta para os links do e-mail.</summary>
    public string? AppUrl { get; init; }
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _o = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.Username)
        && !string.IsNullOrWhiteSpace(_o.Password) && !string.IsNullOrWhiteSpace(_o.From);

    public string? From => _o.From;
    public string? AppUrl => _o.AppUrl;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new EmailSendException("Envio de e-mail não configurado.");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_o.From!, _o.FromName),
            Subject = message.Subject,
            Body = message.Text,
            IsBodyHtml = false,
        };
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, MediaTypeNames.Text.Html));
        // Cada destinatario em copia oculta: ninguem ve o e-mail dos outros.
        mail.To.Add(new MailAddress(_o.From!, _o.FromName));
        foreach (var to in message.To)
        {
            mail.Bcc.Add(to);
        }

        using var smtp = new SmtpClient(_o.Host, _o.Port)
        {
            EnableSsl = _o.EnableSsl,
            Credentials = new NetworkCredential(_o.Username, _o.Password),
            Timeout = 30_000,
        };
        try
        {
            await smtp.SendMailAsync(mail, cancellationToken);
        }
        catch (SmtpException ex)
        {
            throw new EmailSendException(ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new EmailSendException(ex.Message, ex);
        }
    }
}
