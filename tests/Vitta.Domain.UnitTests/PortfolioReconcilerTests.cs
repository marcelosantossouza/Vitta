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

public class PortfolioReconcilerMixedSourcesTests
{
    [Fact]
    public void PastedTableThenCommissionStatement_MatchesByCpf_WithoutFalseExits()
    {
        // Outubro: tabela copiada do site (sem nº da proposta). Novembro: extrato de comissoes (com proposta, sem status).
        PortfolioRow[] october =
        [
            new(null, "ANA LIMA", "52998224725", null, 1643.68m, "Ativo") { StartDate = new DateOnly(2017, 5, 22) },
            new(null, "BRUNO REIS", "11144477735", null, 268.29m, "Ativo") { StartDate = new DateOnly(2024, 8, 1) },
            new(null, "CARLA MENDES", "39053344705", null, 94.39m, "Ativo") { StartDate = new DateOnly(2008, 12, 10) },
        ];
        PortfolioRow[] november =
        [
            new("900000001", "ANA LIMA", "52998224725", "CAPITAL SEGURADO", 1643.68m, null),
            new("900000002", "BRUNO REIS", "11144477735", "VIDA INTEIRA + SAF", 300m, null) { Coverages = 2 },
            new("900000009", "DANIEL COSTA", "12345678909", "VIDA INTEIRA", 50m, null),
        ];

        var changes = PortfolioReconciler.Reconcile(october, november);

        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Changed && c.HolderName == "BRUNO REIS" && c.Detail!.Contains("Prêmio"));
        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Left && c.HolderName == "CARLA MENDES");
        Assert.Contains(changes, c => c.Type == PortfolioChangeType.Entered && c.HolderName == "DANIEL COSTA");
        Assert.DoesNotContain(changes, c => c.HolderName == "ANA LIMA"); // status ausente no extrato nao conta como mudanca
    }
}
