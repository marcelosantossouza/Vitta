using Vitta.Domain.Common;

namespace Vitta.Domain.Referrals;

/// <summary>
/// Grupo de indicacao: segurados que vieram por uma pessoa, empresa, sindicato ou codigo. O dono da corretora
/// "carimba" os clientes com o grupo e acompanha o resultado mensal (com um percentual de nome livre, ex.: "Repasse").
/// </summary>
public sealed class ReferralGroup : AggregateRoot
{
    public const string DefaultShareLabel = "Repasse";

    private ReferralGroup() { } // EF

    /// <summary>Nome da pessoa/empresa ou um codigo (ex.: "SINDPROF-RJ").</summary>
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Percentual sobre a comissao da corretora (10 = 10%). Nulo = sem percentual.</summary>
    public decimal? SharePercent { get; private set; }

    /// <summary>Como o percentual aparece nos relatorios (Repasse, Bonificação, Participação...).</summary>
    public string ShareLabel { get; private set; } = DefaultShareLabel;

    public static Result<ReferralGroup> Create(string name, string? code, string? notes, decimal? sharePercent, string? shareLabel)
    {
        var group = new ReferralGroup();
        var result = group.Update(name, code, notes, sharePercent, shareLabel);
        return result.IsFailure ? result.Error : group;
    }

    public Result Update(string name, string? code, string? notes, decimal? sharePercent, string? shareLabel)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ReferralErrors.NameRequired;
        }

        if (sharePercent is < 0 or > 100)
        {
            return ReferralErrors.InvalidShare;
        }

        Name = Clip(name.Trim(), 120);
        Code = string.IsNullOrWhiteSpace(code) ? null : Clip(code.Trim().ToUpperInvariant(), 30);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : Clip(notes.Trim(), 1000);
        SharePercent = sharePercent is null or 0 ? null : Math.Round(sharePercent.Value, 2);
        ShareLabel = string.IsNullOrWhiteSpace(shareLabel) ? DefaultShareLabel : Clip(shareLabel.Trim(), 40);
        Touch();
        return Result.Success();
    }

    /// <summary>Valor do percentual sobre uma comissao.</summary>
    public static decimal? ShareOf(decimal? commission, decimal? percent) =>
        commission is { } c && percent is { } p ? Math.Round(c * p / 100m, 2, MidpointRounding.AwayFromZero) : null;

    private static string Clip(string v, int max) => v.Length > max ? v[..max] : v;
}

public static class ReferralErrors
{
    public static readonly Error NameRequired = Error.Validation("Referral.NameRequired", "Informe o nome ou código da indicação.");
    public static readonly Error InvalidShare = Error.Validation("Referral.InvalidShare", "O percentual deve ficar entre 0% e 100%.");
    public static readonly Error CodeAlreadyExists = Error.Conflict("Referral.CodeAlreadyExists", "Já existe uma indicação com este código.");
    public static Error NotFound => Error.NotFound("Referral.NotFound", "Indicação não encontrada.");
}
