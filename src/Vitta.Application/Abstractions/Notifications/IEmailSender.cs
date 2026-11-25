namespace Vitta.Application.Abstractions.Notifications;

/// <summary>Envio de e-mail (SMTP em Vitta.Infrastructure). Os dados do servidor ficam no ambiente (.env).</summary>
public interface IEmailSender
{
    /// <summary>Servidor, usuario, senha e remetente configurados.</summary>
    bool IsConfigured { get; }

    /// <summary>Remetente exibido na tela (ex.: vitta@seudominio.com.br).</summary>
    string? From { get; }

    /// <summary>Endereco publico da Vitta, para os links do e-mail (ex.: https://vitta.seudominio.com.br).</summary>
    string? AppUrl { get; }

    /// <summary>Lanca <see cref="EmailSendException"/> quando o servidor recusa ou nao responde.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string Html, string Text);

public sealed class EmailSendException(string message, Exception? inner = null) : Exception(message, inner);
