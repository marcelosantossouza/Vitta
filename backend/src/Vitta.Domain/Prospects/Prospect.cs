using Vitta.Domain.Common;

namespace Vitta.Domain.Prospects;

public enum ProspectStage
{
    New = 1,
    Contacted = 2,
    MeetingScheduled = 3,
    ProposalSent = 4,
    Won = 5,
    Lost = 6,
}

/// <summary>Possivel cliente no funil de prospeccao.</summary>
public sealed class Prospect : AggregateRoot
{
    private Prospect() { } // EF

    private Prospect(string name, string? phone, string? email, string? source)
    {
        Name = name;
        Phone = phone;
        Email = email;
        Source = source;
        Stage = ProspectStage.New;
    }

    public string Name { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    /// <summary>Origem do lead (indicacao, Instagram, evento...).</summary>
    public string? Source { get; private set; }
    public ProspectStage Stage { get; private set; }
    public DateOnly? NextFollowUpOn { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>Preenchido quando o prospect vira cliente.</summary>
    public Guid? ConvertedClientId { get; private set; }

    public static Result<Prospect> Create(string name, string? phone, string? email, string? source) =>
        string.IsNullOrWhiteSpace(name)
            ? Error.Validation("Prospect.NameRequired", "Nome e obrigatorio.")
            : new Prospect(name.Trim(), phone, email, source);

    public void MoveTo(ProspectStage stage, DateOnly? nextFollowUpOn = null)
    {
        Stage = stage;
        NextFollowUpOn = nextFollowUpOn;
        Touch();
    }

    public void MarkConverted(Guid clientId)
    {
        ConvertedClientId = clientId;
        MoveTo(ProspectStage.Won);
    }
}
