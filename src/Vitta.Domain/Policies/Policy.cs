using Vitta.Domain.Common;

namespace Vitta.Domain.Policies;

public enum PolicyStatus
{
    Active = 1,
    /// <summary>Cancelada a pedido do cliente.</summary>
    Cancelled = 2,
    /// <summary>Caducada por falta de pagamento.</summary>
    Lapsed = 3,
    /// <summary>Corretagem transferida para outro corretor (possivel "roubo" de cliente).</summary>
    BrokerChanged = 4,
}

/// <summary>Apolice / proposta de seguro de vida de um cliente em uma seguradora.</summary>
public sealed class Policy : AggregateRoot
{
    private Policy() { } // EF

    private Policy(Guid clientId, Guid insurerId, string policyNumber, string productName, decimal monthlyPremium, DateOnly issuedOn)
    {
        ClientId = clientId;
        InsurerId = insurerId;
        PolicyNumber = policyNumber;
        ProductName = productName;
        MonthlyPremium = monthlyPremium;
        IssuedOn = issuedOn;
        Status = PolicyStatus.Active;
    }

    public Guid ClientId { get; private set; }
    public Guid InsurerId { get; private set; }
    public string PolicyNumber { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public decimal MonthlyPremium { get; private set; }
    public DateOnly IssuedOn { get; private set; }
    public PolicyStatus Status { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public string? EndReason { get; private set; }

    /// <summary>Percentual de comissao do corretor sobre o premio (12.5 = 12,5%). Nulo = ainda nao informado.</summary>
    public decimal? CommissionRate { get; private set; }

    /// <summary>Quanto o corretor recebe por mes desta apolice (premio x percentual).</summary>
    public decimal? MonthlyCommission => CommissionOf(MonthlyPremium, CommissionRate);

    public static decimal? CommissionOf(decimal premium, decimal? rate) =>
        rate is { } r ? Math.Round(premium * r / 100m, 2, MidpointRounding.AwayFromZero) : null;

    /// <summary>Planilhas trazem 12% como "12" ou, em celula de porcentagem do Excel, como 0.12. Valores ate 1 viram percentual.</summary>
    public static decimal? NormalizeRate(decimal? rate) =>
        rate switch
        {
            null => null,
            <= 0 => null,
            <= 1 => Math.Round(rate.Value * 100m, 4),
            _ => Math.Round(rate.Value, 4),
        };

    public static Result<Policy> Create(
        Guid clientId, Guid insurerId, string policyNumber, string productName, decimal monthlyPremium, DateOnly issuedOn, decimal? commissionRate = null)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return Error.Validation("Policy.NumberRequired", "Número da apólice é obrigatório.");
        }

        if (monthlyPremium < 0)
        {
            return PolicyErrors.InvalidPremium;
        }

        if (!ValidRate(commissionRate))
        {
            return PolicyErrors.InvalidCommissionRate;
        }

        return new Policy(clientId, insurerId, policyNumber.Trim(), productName.Trim(), monthlyPremium, issuedOn) { CommissionRate = commissionRate };
    }

    public Result Update(string productName, decimal monthlyPremium, decimal? commissionRate)
    {
        if (monthlyPremium < 0)
        {
            return PolicyErrors.InvalidPremium;
        }

        if (!ValidRate(commissionRate))
        {
            return PolicyErrors.InvalidCommissionRate;
        }

        ProductName = productName.Trim();
        MonthlyPremium = monthlyPremium;
        CommissionRate = commissionRate;
        Touch();
        return Result.Success();
    }

    /// <summary>
    /// Atualiza premio e percentual (ja normalizado, ver <see cref="NormalizeRate"/>) com o que a seguradora informou na
    /// carteira do mes. So muda o que veio preenchido. Devolve true quando algo mudou.
    /// </summary>
    public bool SyncFromCarrier(decimal? monthlyPremium, decimal? rate)
    {
        var changed = false;
        if (monthlyPremium is > 0 and var premium && premium != MonthlyPremium)
        {
            MonthlyPremium = premium;
            changed = true;
        }

        if (rate is not null && ValidRate(rate) && rate != CommissionRate)
        {
            CommissionRate = rate;
            changed = true;
        }

        if (changed)
        {
            Touch();
        }

        return changed;
    }

    // Comissao de primeiro ano em seguro de vida pode passar de 100%; acima de 300% e quase certamente erro de digitacao.
    private static bool ValidRate(decimal? rate) => rate is null or (>= 0 and <= 300);

    public Result End(PolicyStatus newStatus, DateOnly endedOn, string? reason)
    {
        if (Status != PolicyStatus.Active)
        {
            return PolicyErrors.NotActive;
        }

        if (newStatus == PolicyStatus.Active)
        {
            return Error.Validation("Policy.InvalidStatus", "Use Reactivate para reativar.");
        }

        Status = newStatus;
        EndedOn = endedOn;
        EndReason = reason;
        Touch();
        Raise(new PolicyEndedEvent(Id, ClientId, newStatus, DateTimeOffset.UtcNow));
        return Result.Success();
    }

    public void Reactivate()
    {
        Status = PolicyStatus.Active;
        EndedOn = null;
        EndReason = null;
        Touch();
    }
}

public static class PolicyErrors
{
    public static readonly Error InvalidPremium = Error.Validation("Policy.InvalidPremium", "Prêmio não pode ser negativo.");
    public static readonly Error InvalidCommissionRate = Error.Validation("Policy.InvalidCommissionRate", "Percentual de comissão deve ficar entre 0% e 300%.");
    public static readonly Error NotActive = Error.Conflict("Policy.NotActive", "Esta apólice não está ativa.");
    public static readonly Error NumberAlreadyExists = Error.Conflict("Policy.NumberAlreadyExists", "Já existe uma apólice com este número nesta seguradora.");
    public static Error NotFound(Guid id) => Error.NotFound("Policy.NotFound", "Apólice não encontrada.");
}

public sealed record PolicyEndedEvent(Guid PolicyId, Guid ClientId, PolicyStatus Status, DateTimeOffset OccurredAt) : IDomainEvent;
