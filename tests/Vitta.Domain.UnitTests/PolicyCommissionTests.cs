using Vitta.Domain.Policies;

namespace Vitta.Domain.UnitTests;

public class PolicyCommissionTests
{
    private static Policy NewPolicy(decimal premium = 200m, decimal? rate = null) =>
        Policy.Create(Guid.NewGuid(), Guid.NewGuid(), "123", "Vida", premium, new DateOnly(2025, 1, 10), rate).Value;

    [Fact]
    public void MonthlyCommission_IsPremiumTimesRate()
    {
        Assert.Equal(25.30m, NewPolicy(253m, 10m).MonthlyCommission);
        Assert.Null(NewPolicy().MonthlyCommission);
    }

    [Theory]
    [InlineData(0.12, 12)]
    [InlineData(12, 12)]
    [InlineData(1, 100)]
    [InlineData(150, 150)]
    public void NormalizeRate_AcceptsPercentOrExcelFraction(double input, double expected) =>
        Assert.Equal((decimal)expected, Policy.NormalizeRate((decimal)input));

    [Fact]
    public void RateOutOfRange_IsRejected()
    {
        Assert.Equal(PolicyErrors.InvalidCommissionRate, NewPolicy().Update("Vida", 100m, 301m).Error);
        Assert.True(Policy.Create(Guid.NewGuid(), Guid.NewGuid(), "1", "Vida", 1m, new DateOnly(2025, 1, 1), -1m).IsFailure);
    }

    [Fact]
    public void SyncFromCarrier_OnlyChangesWhatCameFilled()
    {
        var policy = NewPolicy(200m, 10m);

        Assert.False(policy.SyncFromCarrier(null, null));
        Assert.True(policy.SyncFromCarrier(210m, null));
        Assert.Equal((210m, 10m), (policy.MonthlyPremium, policy.CommissionRate!.Value));
        Assert.True(policy.SyncFromCarrier(null, 12.5m));
        Assert.Equal(12.5m, policy.CommissionRate);
        Assert.False(policy.SyncFromCarrier(210m, 12.5m));
    }
}
