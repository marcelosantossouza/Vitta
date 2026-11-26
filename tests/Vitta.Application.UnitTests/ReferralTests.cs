using Vitta.Application.Features.Referrals;
using Vitta.Domain.Policies;
using Vitta.Domain.Referrals;

namespace Vitta.Application.UnitTests;

public class ReferralTests
{
    private static readonly DateOnly Nov = new(2026, 11, 1);

    private static ReferralPolicyData P(Guid client, decimal premium, decimal? rate, DateOnly issued,
        PolicyStatus status = PolicyStatus.Active, DateOnly? ended = null) =>
        new(Guid.NewGuid(), client, "MAG", Guid.NewGuid().ToString()[..6], "Vida Inteira", premium, rate, issued, status, ended);

    [Fact]
    public void Report_CountsActiveNewAndCancelled_AndAppliesShareOnCommission()
    {
        var group = ReferralGroup.Create("Sindicato dos Professores", "sindprof", null, 10m, "Bonificação").Value;
        Guid ana = Guid.NewGuid(), bia = Guid.NewGuid(), cid = Guid.NewGuid();
        var data = new ReferralData([group],
            [new(ana, "Ana", "11111111111", group.Id), new(bia, "Bia", "22222222222", group.Id), new(cid, "Cid", "33333333333", group.Id)],
            [
                P(ana, 200m, 50m, new(2026, 3, 10)),                                                // ativa desde marco
                P(bia, 100m, 40m, new(2026, 11, 5)),                                                // nova em novembro
                P(cid, 300m, 30m, new(2026, 1, 2), PolicyStatus.Cancelled, new DateOnly(2026, 11, 20)), // cancelada em novembro
            ],
            []);

        var r = ReferralReport.Build(group, data, new DateOnly(2026, 11, 26));

        Assert.Equal(("SINDPROF", "Bonificação", 10m), (r.Group.Code, r.Group.ShareLabel, r.Group.SharePercent!.Value));
        Assert.Equal((2, 1, 1), (r.ActiveMembers, r.NewPolicies, r.CancelledPolicies));
        Assert.Equal(300m, r.Premium);                       // 200 + 100 (a cancelada nao entra)
        Assert.Equal(140m, r.Commission);                    // 100 + 40
        Assert.Equal("Apólices", r.CommissionBase);
        Assert.Equal(14m, r.Share);                          // 10% de 140
        Assert.Equal(["Ana", "Bia", "Cid"], r.Members.Select(m => m.Name));
        Assert.Equal("Cancelado", r.Members[2].Status);
        Assert.Equal("Nova", r.Members[1].Policies[0].Status);

        var oct = r.History.Single(h => h.Month == new DateOnly(2026, 10, 1));
        Assert.Equal((2, 500m, 190m), (oct.ActiveMembers, oct.Premium, oct.Commission!.Value)); // Ana + Cid em outubro
        Assert.Equal(12, r.History.Count);
    }

    [Fact]
    public void Report_PrefersCommissionFromInsurerSheets_WhenImported()
    {
        var group = ReferralGroup.Create("Empresa X", null, null, 20m, null).Value;
        var ana = Guid.NewGuid();
        var data = new ReferralData([group], [new(ana, "Ana", "11111111111", group.Id)],
            [P(ana, 200m, 50m, new(2026, 3, 10))],
            [new("11111111111", Nov, 87.50m), new("99999999999", Nov, 1000m)]); // outro CPF nao entra

        var r = ReferralReport.Build(group, data, Nov);

        Assert.Equal(("Planilhas", 87.50m, 17.50m), (r.CommissionBase, r.Commission!.Value, r.Share!.Value));
        Assert.Equal(100m, r.EstimatedCommission);
        Assert.Equal(ReferralGroup.DefaultShareLabel, r.Group.ShareLabel);
    }

    [Fact]
    public void Group_ValidatesNameAndPercent()
    {
        Assert.True(ReferralGroup.Create(" ", null, null, null, null).IsFailure);
        Assert.True(ReferralGroup.Create("João", null, null, 150m, null).IsFailure);
        Assert.Null(ReferralGroup.Create("João", null, null, 0m, null).Value.SharePercent);
    }

    [Fact]
    public async Task Assign_StampsClients_AndDeleteRemovesStamp()
    {
        var group = ReferralGroup.Create("João", null, null, null, null).Value;
        var groups = new FakeReferralGroupRepository();
        groups.Add(group);
        var clients = new FakeClientRepository();
        var a = Vitta.Domain.Clients.Client.Create("Ana", "529.982.247-25", null, null, null).Value;
        clients.Add(a);

        var n = await new AssignReferralHandler(groups, clients, new FakeUnitOfWork()).Handle(new AssignReferralCommand(group.Id, [a.Id]), default);
        Assert.Equal((1, group.Id), (n.Value, a.ReferralGroupId!.Value));

        await new DeleteReferralGroupHandler(groups, clients, new FakeUnitOfWork()).Handle(new DeleteReferralGroupCommand(group.Id), default);
        Assert.Null(a.ReferralGroupId);
        Assert.Empty(groups.Groups);
    }

    private sealed class FakeReferralGroupRepository : Vitta.Application.Abstractions.Persistence.IReferralGroupRepository
    {
        public List<ReferralGroup> Groups { get; } = [];
        public Task<ReferralGroup?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Groups.FirstOrDefault(g => g.Id == id));
        public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default) =>
            Task.FromResult(Groups.Any(g => g.Code == code && g.Id != exceptId));
        public void Add(ReferralGroup group) => Groups.Add(group);
        public void Remove(ReferralGroup group) => Groups.Remove(group);
    }
}
