using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;

namespace Vitta.Domain.UnitTests;

public class PortfolioImportTests
{
    private static readonly Guid Mag = Guid.NewGuid();

    [Fact]
    public void FirstImport_IsBaseline_WithoutChanges()
    {
        var import = PortfolioImport.Create(Mag, new DateOnly(2026, 9, 15), "set.xlsx");

        import.Process([new("1", "Ana", null, null, 100m, "Ativa")], previous: null);

        Assert.True(import.IsFirstImport);
        Assert.Empty(import.Changes);
        Assert.Equal(new DateOnly(2026, 9, 1), import.ReferenceMonth);
        Assert.Single(import.Rows);
    }

    [Fact]
    public void NextImport_ComparesWithPreviousSnapshot()
    {
        var september = PortfolioImport.Create(Mag, new DateOnly(2026, 9, 1), "set.xlsx");
        september.Process([new("1", "Ana", null, null, 100m, "Ativa"), new("2", "Bruno", null, null, 50m, "Ativa")], null);

        var october = PortfolioImport.Create(Mag, new DateOnly(2026, 10, 1), "out.xlsx");
        october.Process([new("1", "Ana", null, null, 100m, "Ativa")], september);

        Assert.False(october.IsFirstImport);
        var change = Assert.Single(october.Changes);
        Assert.Equal(PortfolioChangeType.Left, change.Type);
        Assert.Equal("Bruno", change.HolderName);
    }
}

public class ProspectTests
{
    [Fact]
    public void MoveToClosedStage_ClearsFollowUp()
    {
        var prospect = Prospect.Create("Ana", null, null, null, null, new DateOnly(2026, 11, 20)).Value;

        prospect.MoveTo(ProspectStage.Lost, new DateOnly(2026, 12, 1));

        Assert.Null(prospect.NextFollowUpOn);
        Assert.False(prospect.IsOpen);
    }
}
