using Vitta.Domain.Portfolio;

namespace Vitta.Domain.UnitTests;

public class PortfolioReconcilerTests
{
    [Fact]
    public void Reconcile_DetectsEnteredLeftAndChanged()
    {
        PortfolioRow[] previous =
        [
            new("001", "Ana", null, "Vida", 100m, "Ativa"),
            new("002", "Bruno", null, "Vida", 200m, "Ativa"),
        ];
        PortfolioRow[] current =
        [
            new("1", "Ana", null, "Vida", 120m, "Ativa"), // zeros a esquerda ignorados; premio mudou
            new("003", "Carla", null, "Vida", 80m, "Ativa"),
        ];

        var changes = PortfolioReconciler.Reconcile(previous, current);

        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Changed && c.HolderName == "Ana");
        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Entered && c.HolderName == "Carla");
        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Left && c.HolderName == "Bruno");
        Assert.Equal(3, changes.Count);
    }
}
