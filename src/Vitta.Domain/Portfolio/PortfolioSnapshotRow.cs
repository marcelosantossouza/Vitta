using Vitta.Domain.Common;

namespace Vitta.Domain.Portfolio;

/// <summary>Linha da planilha guardada como retrato da carteira daquele mes.</summary>
public sealed class PortfolioSnapshotRow : Entity
{
    private PortfolioSnapshotRow() { } // EF

    public string PolicyNumber { get; private set; } = null!;
    public string HolderName { get; private set; } = null!;
    public string? HolderCpf { get; private set; }
    public string? ProductName { get; private set; }
    public decimal? MonthlyPremium { get; private set; }
    public string? Status { get; private set; }

    public static PortfolioSnapshotRow From(PortfolioRow row) => new()
    {
        PolicyNumber = row.PolicyNumber,
        HolderName = row.HolderName,
        HolderCpf = row.HolderCpf,
        ProductName = row.ProductName,
        MonthlyPremium = row.MonthlyPremium,
        Status = row.Status,
    };

    public PortfolioRow ToRow() => new(PolicyNumber, HolderName, HolderCpf, ProductName, MonthlyPremium, Status);
}
