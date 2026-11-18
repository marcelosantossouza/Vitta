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

    public static Result<Policy> Create(Guid clientId, Guid insurerId, string policyNumber, string productName, decimal monthlyPremium, DateOnly issuedOn)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return Error.Validation("Policy.NumberRequired", "Numero da apolice e obrigatorio.");
        }

        if (monthlyPremium < 0)
        {
            return Error.Validation("Policy.InvalidPremium", "Premio nao pode ser negativo.");
        }

        return new Policy(clientId, insurerId, policyNumber.Trim(), productName.Trim(), monthlyPremium, issuedOn);
    }

    public Result End(PolicyStatus newStatus, DateOnly endedOn, string? reason)
    {
        if (Status != PolicyStatus.Active)
        {
            return Error.Conflict("Policy.NotActive", "Apolice nao esta ativa.");
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

public sealed record PolicyEndedEvent(Guid PolicyId, Guid ClientId, PolicyStatus Status, DateTimeOffset OccurredAt) : IDomainEvent;
