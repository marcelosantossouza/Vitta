using System.Globalization;

namespace Vitta.Domain.Portfolio;

/// <summary>
/// Linha normalizada de uma carteira, independente da fonte (planilha da seguradora, extrato de comissoes ou
/// tabela copiada do site). Varias linhas da mesma proposta (uma por cobertura) ja chegam somadas aqui.
/// </summary>
/// <param name="PolicyNumber">Nº da apolice/proposta; nulo quando a fonte nao traz (ex.: tabela copiada do site).</param>
public sealed record PortfolioRow(
    string? PolicyNumber,
    string HolderName,
    string? HolderCpf,
    string? ProductName,
    decimal? MonthlyPremium,
    string? Status)
{
    public DateOnly? StartDate { get; init; }
    public string? City { get; init; }
    /// <summary>Mes de competencia informado no arquivo (primeiro dia do mes).</summary>
    public DateOnly? Competence { get; init; }
    /// <summary>Numero da parcela (ex.: 26 = 26ª mensalidade).</summary>
    public int? Installment { get; init; }
    public decimal? CommissionAmount { get; init; }
    /// <summary>Percentual de comissao (12 = 12%).</summary>
    public decimal? CommissionRate { get; init; }
    /// <summary>Quantas linhas/coberturas da fonte foram somadas nesta linha.</summary>
    public int Coverages { get; init; } = 1;
    /// <summary>Outras colunas uteis ("Coluna: valor" por linha).</summary>
    public string? Extras { get; init; }

    /// <summary>Como a linha aparece para o usuario: nº da apolice ou, sem ele, CPF + inicio da vigencia.</summary>
    public string DisplayId => PolicyNumber ?? CpfLabel(HolderCpf, StartDate) ?? "(sem identificação)";

    public static string? CpfLabel(string? cpfDigits, DateOnly? startDate)
    {
        if (string.IsNullOrEmpty(cpfDigits) || cpfDigits.Length != 11)
        {
            return null;
        }

        var label = $"CPF {cpfDigits[..3]}.{cpfDigits[3..6]}.{cpfDigits[6..9]}-{cpfDigits[9..]}";
        return startDate is { } d ? $"{label} · {d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" : label;
    }
}

/// <summary>
/// Servico de dominio que compara a carteira do mes anterior com a atual e gera as mudancas.
/// E aqui que nasce o alerta de "cliente saiu" para investigar possivel roubo de carteira.
/// </summary>
/// <remarks>
/// As fontes podem variar de um mes para o outro (ex.: MAG pelo extrato de comissoes, com nº da proposta, ou pela
/// tabela do site, sem ele). Por isso o casamento e feito em etapas:
/// 1) nº da apolice, quando os dois lados tem; 2) CPF + inicio da vigencia; 3) CPF do cliente (comparando o conjunto
/// de apolices dele). O que sobra entrou ou saiu.
/// </remarks>
public static class PortfolioReconciler
{
    public static IReadOnlyList<PortfolioChange> Reconcile(IEnumerable<PortfolioRow> previous, IEnumerable<PortfolioRow> current)
    {
        var before = previous.ToList();
        var after = current.ToList();
        var matchedBefore = new HashSet<int>();
        var matchedAfter = new HashSet<int>();
        var changes = new List<PortfolioChange>();

        // 1) Nº da apolice (zeros a esquerda e maiusculas nao importam).
        var byPolicy = Index(before, r => r.PolicyNumber is { } p ? PolicyKey(p) : null);
        for (var i = 0; i < after.Count; i++)
        {
            if (after[i].PolicyNumber is { } p && byPolicy.TryGetValue(PolicyKey(p), out var j) && matchedBefore.Add(j))
            {
                matchedAfter.Add(i);
                Compare(before[j], after[i], changes);
            }
        }

        // 2) CPF + inicio da vigencia.
        var byCpfDate = Index(before, (r, j) => !matchedBefore.Contains(j) && r.HolderCpf is { } c && r.StartDate is { } d ? $"{c}|{d:yyyyMMdd}" : null);
        for (var i = 0; i < after.Count; i++)
        {
            var r = after[i];
            if (!matchedAfter.Contains(i) && r.HolderCpf is { } c && r.StartDate is { } d
                && byCpfDate.TryGetValue($"{c}|{d:yyyyMMdd}", out var j) && matchedBefore.Add(j))
            {
                matchedAfter.Add(i);
                Compare(before[j], after[i], changes);
            }
        }

        // 3) CPF: compara o conjunto de apolices do cliente (fontes com granularidades diferentes).
        var oldByCpf = Group(before, matchedBefore);
        var newByCpf = Group(after, matchedAfter);
        foreach (var (cpf, newRows) in newByCpf)
        {
            if (!oldByCpf.TryGetValue(cpf, out var oldRows))
            {
                continue;
            }

            CompareClient(oldRows.Select(x => before[x]).ToList(), newRows.Select(x => after[x]).ToList(), changes);
            oldRows.ForEach(x => matchedBefore.Add(x));
            newRows.ForEach(x => matchedAfter.Add(x));
        }

        for (var i = 0; i < after.Count; i++)
        {
            if (!matchedAfter.Contains(i))
            {
                var row = after[i];
                changes.Add(new PortfolioChange(PortfolioChangeType.Entered, row.DisplayId, row.HolderName, row.HolderCpf, row.ProductName));
            }
        }

        for (var j = 0; j < before.Count; j++)
        {
            if (!matchedBefore.Contains(j))
            {
                var row = before[j];
                changes.Add(new PortfolioChange(PortfolioChangeType.Left, row.DisplayId, row.HolderName, row.HolderCpf,
                    row.Status is null ? "Não consta mais na carteira" : $"Não consta mais na carteira (último status: {row.Status})"));
            }
        }

        return changes;
    }

    private static void Compare(PortfolioRow old, PortfolioRow row, List<PortfolioChange> changes)
    {
        var diffs = new List<string>();
        // So compara o que as duas fontes informam (o extrato de comissoes, por exemplo, nao traz status).
        if (old.Status is not null && row.Status is not null && !string.Equals(old.Status, row.Status, StringComparison.OrdinalIgnoreCase))
        {
            diffs.Add($"Status: {old.Status} → {row.Status}");
        }

        if (old.MonthlyPremium is { } a && row.MonthlyPremium is { } b && a != b)
        {
            diffs.Add($"Prêmio: {Money(a)} → {Money(b)}");
        }

        if (diffs.Count > 0)
        {
            changes.Add(new PortfolioChange(PortfolioChangeType.Changed, row.DisplayId, row.HolderName, row.HolderCpf, string.Join("; ", diffs)));
        }
    }

    private static void CompareClient(List<PortfolioRow> old, List<PortfolioRow> current, List<PortfolioChange> changes)
    {
        var diffs = new List<string>();
        if (old.Count != current.Count)
        {
            diffs.Add($"Apólices: {old.Count} → {current.Count}");
        }

        var oldStatus = Statuses(old);
        var newStatus = Statuses(current);
        if (oldStatus is not null && newStatus is not null && !string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            diffs.Add($"Status: {oldStatus} → {newStatus}");
        }

        if (old.All(r => r.MonthlyPremium is not null) && current.All(r => r.MonthlyPremium is not null))
        {
            var a = old.Sum(r => r.MonthlyPremium!.Value);
            var b = current.Sum(r => r.MonthlyPremium!.Value);
            if (a != b)
            {
                diffs.Add($"Prêmio: {Money(a)} → {Money(b)}");
            }
        }

        if (diffs.Count > 0)
        {
            var row = current[0];
            changes.Add(new PortfolioChange(PortfolioChangeType.Changed, PortfolioRow.CpfLabel(row.HolderCpf, null) ?? row.DisplayId,
                row.HolderName, row.HolderCpf, string.Join("; ", diffs)));
        }
    }

    private static string? Statuses(List<PortfolioRow> rows)
    {
        var values = rows.Select(r => r.Status).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static Dictionary<string, int> Index(List<PortfolioRow> rows, Func<PortfolioRow, string?> key) =>
        Index(rows, (r, _) => key(r));

    private static Dictionary<string, int> Index(List<PortfolioRow> rows, Func<PortfolioRow, int, string?> key)
    {
        var index = new Dictionary<string, int>();
        for (var j = 0; j < rows.Count; j++)
        {
            if (key(rows[j], j) is { } k)
            {
                index[k] = j; // repetido: vale a ultima linha
            }
        }

        return index;
    }

    private static Dictionary<string, List<int>> Group(List<PortfolioRow> rows, HashSet<int> matched) =>
        Enumerable.Range(0, rows.Count)
            .Where(i => !matched.Contains(i) && rows[i].HolderCpf is not null)
            .GroupBy(i => rows[i].HolderCpf!)
            .ToDictionary(g => g.Key, g => g.ToList());

    private static readonly CultureInfo PtBr = new("pt-BR");

    private static string Money(decimal value) => value.ToString("C", PtBr);

    private static string PolicyKey(string policyNumber) => policyNumber.Trim().TrimStart('0').ToUpperInvariant();
}
