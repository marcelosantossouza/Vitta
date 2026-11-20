namespace Vitta.Domain.Portfolio;

/// <summary>Linha normalizada de uma planilha de carteira, independente do layout da seguradora.</summary>
public sealed record PortfolioRow(
    string PolicyNumber,
    string HolderName,
    string? HolderCpf,
    string? ProductName,
    decimal? MonthlyPremium,
    string? Status);

/// <summary>
/// Servico de dominio que compara a carteira do mes anterior com a atual e gera as mudancas.
/// E aqui que nasce o alerta de "cliente saiu" para investigar possivel roubo de carteira.
/// </summary>
public static class PortfolioReconciler
{
    public static IReadOnlyList<PortfolioChange> Reconcile(IEnumerable<PortfolioRow> previous, IEnumerable<PortfolioRow> current)
    {
        var before = previous.GroupBy(r => Key(r.PolicyNumber)).ToDictionary(g => g.Key, g => g.Last());
        var after = current.GroupBy(r => Key(r.PolicyNumber)).ToDictionary(g => g.Key, g => g.Last());
        var changes = new List<PortfolioChange>();

        foreach (var (key, row) in after)
        {
            if (!before.TryGetValue(key, out var old))
            {
                changes.Add(new PortfolioChange(PortfolioChangeType.Entered, row.PolicyNumber, row.HolderName, row.HolderCpf, row.ProductName));
                continue;
            }

            var diffs = new List<string>();
            if (!string.Equals(old.Status, row.Status, StringComparison.OrdinalIgnoreCase))
            {
                diffs.Add($"Status: {old.Status ?? "-"} → {row.Status ?? "-"}");
            }

            if (old.MonthlyPremium != row.MonthlyPremium)
            {
                diffs.Add($"Prêmio: {Money(old.MonthlyPremium)} → {Money(row.MonthlyPremium)}");
            }

            if (diffs.Count > 0)
            {
                changes.Add(new PortfolioChange(PortfolioChangeType.Changed, row.PolicyNumber, row.HolderName, row.HolderCpf, string.Join("; ", diffs)));
            }
        }

        foreach (var (key, row) in before)
        {
            if (!after.ContainsKey(key))
            {
                changes.Add(new PortfolioChange(PortfolioChangeType.Left, row.PolicyNumber, row.HolderName, row.HolderCpf,
                    row.Status is null ? "Não consta mais na planilha" : $"Não consta mais na planilha (último status: {row.Status})"));
            }
        }

        return changes;
    }

    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    private static string Money(decimal? value) => value?.ToString("C", PtBr) ?? "-";

    private static string Key(string policyNumber) => policyNumber.Trim().TrimStart('0').ToUpperInvariant();
}
