using Vitta.Domain.Common;

namespace Vitta.Domain.Portfolio;

/// <summary>Linha guardada como retrato da carteira daquele mes (a "tabela final" da importacao).</summary>
public sealed class PortfolioSnapshotRow : Entity
{
    private PortfolioSnapshotRow() { } // EF

    /// <summary>Nº da apolice/proposta; nulo quando a fonte nao traz (tabela copiada do site).</summary>
    public string? PolicyNumber { get; private set; }
    public string HolderName { get; private set; } = null!;
    public string? HolderCpf { get; private set; }
    public string? ProductName { get; private set; }
    public decimal? MonthlyPremium { get; private set; }
    public string? Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public string? City { get; private set; }
    public DateOnly? Competence { get; private set; }
    public int? Installment { get; private set; }
    public decimal? CommissionAmount { get; private set; }
    public decimal? CommissionRate { get; private set; }
    public int Coverages { get; private set; } = 1;
    public string? Extras { get; private set; }

    public static PortfolioSnapshotRow From(PortfolioRow row) => new()
    {
        PolicyNumber = row.PolicyNumber,
        HolderName = row.HolderName,
        HolderCpf = row.HolderCpf,
        ProductName = row.ProductName,
        MonthlyPremium = row.MonthlyPremium,
        Status = row.Status,
        StartDate = row.StartDate,
        City = row.City,
        Competence = row.Competence,
        Installment = row.Installment,
        CommissionAmount = row.CommissionAmount,
        CommissionRate = row.CommissionRate,
        Coverages = row.Coverages,
        Extras = row.Extras,
    };

    public PortfolioRow ToRow() => new(PolicyNumber, HolderName, HolderCpf, ProductName, MonthlyPremium, Status)
    {
        StartDate = StartDate,
        City = City,
        Competence = Competence,
        Installment = Installment,
        CommissionAmount = CommissionAmount,
        CommissionRate = CommissionRate,
        Coverages = Coverages,
        Extras = Extras,
    };
}
