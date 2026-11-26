using Vitta.Domain.Common;

namespace Vitta.Domain.Contacts;

public enum ContactChannel
{
    Call = 1,
    WhatsApp = 2,
    Email = 3,
    Visit = 4,
    Meeting = 5,
    /// <summary>Pedido de contato pela pagina de captacao/site.</summary>
    Form = 6,
    Other = 7,
}

/// <summary>
/// Um contato feito com o prospect ou cliente (quem, quando, por onde, o que foi tratado).
/// Na conversao o historico passa do prospect para o cliente.
/// </summary>
public sealed class ContactLog : AggregateRoot
{
    public const int MaxSummary = 2000;

    private ContactLog() { } // EF

    public Guid? ProspectId { get; private set; }
    public Guid? ClientId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public ContactChannel Channel { get; private set; }
    public string Summary { get; private set; } = null!;
    public Guid? UserId { get; private set; }

    /// <summary>Nome de quem registrou, guardado na hora (continua certo se o usuario mudar de nome ou sair).</summary>
    public string? UserName { get; private set; }

    public static Result<ContactLog> Create(
        Guid? prospectId, Guid? clientId, DateTimeOffset occurredAt, ContactChannel channel, string summary, Guid? userId, string? userName)
    {
        if (prospectId is null == clientId is null)
        {
            return Error.Validation("Contact.Target", "O contato precisa ser de um prospect ou de um cliente.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return Error.Validation("Contact.SummaryRequired", "Descreva o que foi tratado.");
        }

        if (!Enum.IsDefined(channel))
        {
            return Error.Validation("Contact.Channel", "Canal inválido.");
        }

        var text = summary.Trim();
        return new ContactLog
        {
            ProspectId = prospectId,
            ClientId = clientId,
            OccurredAt = occurredAt,
            Channel = channel,
            Summary = text.Length > MaxSummary ? text[..MaxSummary] : text,
            UserId = userId,
            UserName = userName is { Length: > 120 } ? userName[..120] : userName,
        };
    }

    /// <summary>Prospect virou cliente: o historico passa a ser do cliente.</summary>
    public void MoveToClient(Guid clientId)
    {
        ClientId = clientId;
        ProspectId = null;
        Touch();
    }

    public static Error NotFound => Error.NotFound("Contact.NotFound", "Contato não encontrado.");
}
