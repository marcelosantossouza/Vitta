using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;

namespace Vitta.Application.Features.Portfolio;

/// <summary>
/// Liga a carteira importada ao cadastro: cada linha que corresponde a uma apolice cadastrada atualiza o premio e o
/// percentual de comissao dela. A correspondencia e pelo numero da apolice (na mesma seguradora); sem numero (ex.: lista
/// colada do site da MAG), pelo CPF, mas so quando o cliente tem UMA apolice ativa nessa seguradora - senao nao da para
/// saber qual e qual.
/// </summary>
public static class PolicyCarrierSync
{
    public static int Apply(IEnumerable<PortfolioRow> rows, IReadOnlyList<PolicySyncCandidate> candidates)
    {
        var byNumber = new Dictionary<string, Policy>();
        foreach (var c in candidates)
        {
            byNumber.TryAdd(NormalizeNumber(c.Policy.PolicyNumber), c.Policy);
        }

        var singleActiveByCpf = candidates
            .Where(c => c.Policy.Status == PolicyStatus.Active)
            .GroupBy(c => Digits(c.ClientCpf))
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Policy);

        var updated = new HashSet<Guid>();
        foreach (var row in rows)
        {
            Policy? target = null;
            if (!string.IsNullOrWhiteSpace(row.PolicyNumber))
            {
                byNumber.TryGetValue(NormalizeNumber(row.PolicyNumber), out target);
            }
            else if (Digits(row.HolderCpf) is { Length: 11 } cpf)
            {
                singleActiveByCpf.TryGetValue(cpf, out target);
            }

            if (target is not null && target.SyncFromCarrier(row.MonthlyPremium, RateOf(row)))
            {
                updated.Add(target.Id);
            }
        }

        return updated.Count;
    }

    /// <summary>Percentual da linha; sem a coluna de %, deduz de comissao / premio (ex.: extrato de comissoes).</summary>
    public static decimal? RateOf(PortfolioRow row)
    {
        if (Policy.NormalizeRate(row.CommissionRate) is { } rate)
        {
            return rate;
        }

        return row is { CommissionAmount: > 0 and var amount, MonthlyPremium: > 0 and var premium }
            ? Math.Round(amount / premium * 100m, 4)
            : null;
    }

    // Excel costuma comer zeros a esquerda: "000123" e "123" sao a mesma apolice.
    private static string NormalizeNumber(string number)
    {
        var trimmed = number.Trim().ToUpperInvariant().TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
