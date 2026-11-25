using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.Portfolio;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;

namespace Vitta.Application.UnitTests;

public class PolicyCarrierSyncTests
{
    private static readonly Guid Insurer = Guid.NewGuid();

    private static PolicySyncCandidate Candidate(string number, string cpf, decimal premium = 100m, decimal? rate = null) =>
        new(Policy.Create(Guid.NewGuid(), Insurer, number, "Vida", premium, new DateOnly(2025, 1, 1), rate).Value, cpf);

    private static PortfolioRow Row(string? number, string? cpf, decimal? premium, decimal? rate = null, decimal? amount = null) =>
        new(number, "Ana", cpf, "Vida", premium, "ATIVA") { CommissionRate = rate, CommissionAmount = amount };

    [Fact]
    public void MatchesByNumber_IgnoringLeadingZeros_AndNormalizesExcelFraction()
    {
        var c = Candidate("000123", "11111111111");

        var updated = PolicyCarrierSync.Apply([Row("123", null, 150m, 0.15m)], [c]);

        Assert.Equal(1, updated);
        Assert.Equal((150m, 15m), (c.Policy.MonthlyPremium, c.Policy.CommissionRate!.Value));
    }

    [Fact]
    public void DerivesRateFromCommissionAmount_WhenNoRateColumn()
    {
        var c = Candidate("9", "11111111111", 200m);

        PolicyCarrierSync.Apply([Row("9", null, 200m, amount: 24m)], [c]);

        Assert.Equal(12m, c.Policy.CommissionRate);
    }

    [Fact]
    public void WithoutNumber_MatchesByCpf_OnlyWhenClientHasOneActivePolicy()
    {
        var single = Candidate("1", "111.111.111-11");
        var twinA = Candidate("2", "22222222222");
        var twinB = Candidate("3", "22222222222");

        var updated = PolicyCarrierSync.Apply(
            [Row(null, "11111111111", 300m, 10m), Row(null, "222.222.222-22", 999m, 50m)], [single, twinA, twinB]);

        Assert.Equal(1, updated);
        Assert.Equal(300m, single.Policy.MonthlyPremium);
        Assert.Equal(100m, twinA.Policy.MonthlyPremium); // ambiguo: nao mexe
        Assert.Null(twinB.Policy.CommissionRate);
    }

    [Fact]
    public void MonthTotals_SumPremiumAndCommission()
    {
        var totals = MonthTotalsDto.From(new DateOnly(2026, 10, 1),
            [Row("1", null, 100m, 10m), Row("2", null, 300m, amount: 60m), Row("3", null, 100m)]);

        Assert.Equal(3, totals.Policies);
        Assert.Equal(500m, totals.Premium);
        Assert.Equal(70m, totals.Commission);   // 10 + 60; a linha sem comissao nao entra
        Assert.Equal(17.5m, totals.AverageRate); // 70 / 400
    }
}
