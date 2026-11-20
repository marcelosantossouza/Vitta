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

    public static Result<Prospect> Create(string name, string? phone, string? email, string? source, string? notes = null, DateOnly? nextFollowUpOn = null)
    {
        var prospect = new Prospect();
        var result = prospect.Update(name, phone, email, source, notes, nextFollowUpOn);
        if (result.IsFailure)
        {
            return result.Error;
        }

        prospect.Stage = ProspectStage.New;
        return prospect;
    }

    public Result Update(string name, string? phone, string? email, string? source, string? notes, DateOnly? nextFollowUpOn)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProspectErrors.NameRequired;
        }

        Name = name.Trim();
        Phone = Clean(phone);
        Email = Clean(email);
        Source = Clean(source);
        Notes = Clean(notes);
        NextFollowUpOn = nextFollowUpOn;
        Touch();
        return Result.Success();
    }

    public bool IsOpen => Stage is not (ProspectStage.Won or ProspectStage.Lost);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void MoveTo(ProspectStage stage, DateOnly? nextFollowUpOn = null)
    {
        Stage = stage;
        NextFollowUpOn = stage is ProspectStage.Won or ProspectStage.Lost ? null : nextFollowUpOn;
        Touch();
    }

    public Result MarkConverted(Guid clientId)
    {
        if (ConvertedClientId is not null)
        {
            return ProspectErrors.AlreadyConverted;
        }

        ConvertedClientId = clientId;
        MoveTo(ProspectStage.Won);
        return Result.Success();
    }
}

public static class ProspectErrors
{
    public static readonly Error NameRequired = Error.Validation("Prospect.NameRequired", "Nome é obrigatório.");
    public static readonly Error AlreadyConverted = Error.Conflict("Prospect.AlreadyConverted", "Este prospect ja foi convertido em cliente.");
    public static Error NotFound(Guid id) => Error.NotFound("Prospect.NotFound", "Prospect não encontrado.");
}
