using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;

namespace Vitta.Application.Features.Portfolio;

/// <summary>O que a importacao fez no cadastro de clientes e apolices.</summary>
/// <param name="SkippedWithoutNumber">Linhas sem nº da apolice que nao bateram com nenhuma apolice (nao da para cadastrar).</param>
/// <param name="SkippedWithoutCpf">Linhas novas sem CPF valido (o cliente precisa de CPF).</param>
public sealed record BookSyncResultDto(int ClientsCreated, int PoliciesCreated, int PoliciesUpdated, int SkippedWithoutNumber, int SkippedWithoutCpf)
{
    public static readonly BookSyncResultDto Empty = new(0, 0, 0, 0, 0);
}

/// <summary>Novos clientes/apolices para o handler adicionar aos repositorios, e o resumo do que foi feito.</summary>
public sealed record BookSyncOutcome(BookSyncResultDto Result, IReadOnlyList<Client> NewClients, IReadOnlyList<Policy> NewPolicies);

/// <summary>
/// Liga a carteira importada ao cadastro. Para cada linha:
/// <list type="bullet">
/// <item>se corresponde a uma apolice cadastrada, atualiza premio e % de comissao. A correspondencia e pelo numero da
/// apolice (na mesma seguradora); sem numero (ex.: lista colada do site da MAG), pelo CPF, mas so quando o cliente tem
/// UMA apolice ativa nessa seguradora - senao nao da para saber qual e qual;</item>
/// <item>senao, cadastra a apolice (e o cliente, se o CPF ainda nao existe). Precisa de nº da apolice e CPF valido.</item>
/// </list>
/// </summary>
public static class PolicyCarrierSync
{
    /// <summary>So atualiza apolices existentes (sem cadastrar). Devolve quantas mudaram.</summary>
    public static int Apply(IEnumerable<PortfolioRow> rows, IReadOnlyList<PolicySyncCandidate> candidates) =>
        Sync(rows, candidates, new Dictionary<string, Guid>(), Guid.Empty, default, create: false).Result.PoliciesUpdated;

    public static BookSyncOutcome Sync(
        IEnumerable<PortfolioRow> rows, IReadOnlyList<PolicySyncCandidate> candidates, IReadOnlyDictionary<string, Guid> clientIdsByCpf,
        Guid insurerId, DateOnly referenceMonth, bool create = true)
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

        var clientIds = new Dictionary<string, Guid>(clientIdsByCpf);
        var newClients = new List<Client>();
        var newPolicies = new List<Policy>();
        var updated = new HashSet<Guid>();
        int withoutNumber = 0, withoutCpf = 0;

        foreach (var row in rows)
        {
            var cpf = Digits(row.HolderCpf);
            var hasNumber = !string.IsNullOrWhiteSpace(row.PolicyNumber);
            Policy? target = null;
            if (hasNumber)
            {
                byNumber.TryGetValue(NormalizeNumber(row.PolicyNumber!), out target);
            }
            else if (cpf.Length == 11)
            {
                singleActiveByCpf.TryGetValue(cpf, out target);
            }

            if (target is not null)
            {
                if (target.SyncFromCarrier(row.MonthlyPremium, RateOf(row)) && !newPolicies.Contains(target))
                {
                    updated.Add(target.Id);
                }

                continue;
            }

            if (!create)
            {
                continue;
            }

            if (!hasNumber)
            {
                withoutNumber++;
                continue;
            }

            if (!clientIds.TryGetValue(cpf, out var clientId))
            {
                var client = Client.Create(row.HolderName, cpf, null, null, null);
                if (client.IsFailure)
                {
                    withoutCpf++;
                    continue;
                }

                newClients.Add(client.Value);
                clientId = client.Value.Id;
                clientIds[cpf] = clientId;
            }

            var rate = RateOf(row);
            var policy = Policy.Create(
                clientId, insurerId, row.PolicyNumber!.Trim(), string.IsNullOrWhiteSpace(row.ProductName) ? "Não informado" : row.ProductName,
                row.MonthlyPremium is > 0 ? row.MonthlyPremium.Value : 0m, row.StartDate ?? referenceMonth, rate is <= 300 ? rate : null);
            if (policy.IsFailure)
            {
                continue;
            }

            newPolicies.Add(policy.Value);
            byNumber[NormalizeNumber(row.PolicyNumber!)] = policy.Value; // a mesma apolice repetida no arquivo nao vira duas
        }

        return new BookSyncOutcome(
            new BookSyncResultDto(newClients.Count, newPolicies.Count, updated.Count, withoutNumber, withoutCpf), newClients, newPolicies);
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
