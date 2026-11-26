using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Common;
using Vitta.Domain.Common;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Features.Prospects;

// Gestao da prospeccao: lista com filtros, dashboard, acoes em lote e historico de importacoes.

/// <summary>Situacao do proximo contato, para filtrar quem precisa ser procurado.</summary>
public enum FollowUpFilter
{
    Any = 0,
    /// <summary>Data ja passou (em aberto).</summary>
    Overdue = 1,
    Today = 2,
    /// <summary>Hoje ou nos proximos 7 dias.</summary>
    Next7Days = 3,
    /// <summary>Sem data agendada (em aberto).</summary>
    None = 4,
}

/// <summary>Filtros da lista e do dashboard (os dois usam os mesmos). Nulos/vazios = sem filtro.</summary>
public sealed record ProspectFilter(
    string? Search = null,
    IReadOnlyList<ProspectStage>? Stages = null,
    string? City = null,
    string? State = null,
    string? Source = null,
    Guid? ImportId = null,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null,
    FollowUpFilter FollowUp = FollowUpFilter.Any,
    bool? HasPhone = null);

public enum ProspectSort
{
    /// <summary>Proximo contato mais cedo primeiro (sem data no fim).</summary>
    FollowUp = 0,
    SalaryDesc = 1,
    SalaryAsc = 2,
    Name = 3,
    Newest = 4,
    City = 5,
}

public sealed record LabelCount(string Label, int Count);

/// <param name="ConversionRate">Ganhos / (ganhos + perdidos), em %; nulo sem nenhum fechado.</param>
public sealed record ProspectInsightsDto(
    int Total, int Open, int NewLast7Days, int Overdue, int DueToday, int Won, int Lost, decimal? ConversionRate,
    decimal? AverageSalary, int WithSalary, IReadOnlyList<LabelCount> ByStage, IReadOnlyList<LabelCount> TopCities,
    IReadOnlyList<LabelCount> BySource, IReadOnlyList<LabelCount> SalaryBands);

public sealed record ProspectImportItemDto(
    Guid Id, string FileName, string TemplateName, DateTimeOffset CreatedAt, int TotalRows, int Created, int Updated, int Skipped,
    int StillLinked, int StillNew);

/// <summary>Valores existentes, para montar os filtros da tela.</summary>
public sealed record ProspectFilterOptionsDto(
    IReadOnlyList<string> Cities, IReadOnlyList<string> States, IReadOnlyList<string> Sources, IReadOnlyList<ProspectImportItemDto> Imports);

// ---------- Lista ----------

public sealed record SearchProspectsQuery(ProspectFilter Filter, ProspectSort Sort = ProspectSort.FollowUp, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<ProspectDto>>;

public sealed class SearchProspectsHandler(IProspectQueries queries, TimeProvider time) : IQueryHandler<SearchProspectsQuery, PagedResult<ProspectDto>>
{
    public async ValueTask<PagedResult<ProspectDto>> Handle(SearchProspectsQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize);
        return await queries.SearchAsync(q.Filter, q.Sort, page, size, time.Today(), ct);
    }
}

// ---------- Dashboard ----------

public sealed record GetProspectInsightsQuery(ProspectFilter Filter) : IQuery<ProspectInsightsDto>;

public sealed class GetProspectInsightsHandler(IProspectQueries queries, TimeProvider time) : IQueryHandler<GetProspectInsightsQuery, ProspectInsightsDto>
{
    public async ValueTask<ProspectInsightsDto> Handle(GetProspectInsightsQuery q, CancellationToken ct) =>
        await queries.InsightsAsync(q.Filter, time.Today(), time.GetUtcNow(), ct);
}

// ---------- Opcoes de filtro ----------

public sealed record GetProspectFilterOptionsQuery : IQuery<ProspectFilterOptionsDto>;

public sealed class GetProspectFilterOptionsHandler(IProspectQueries queries) : IQueryHandler<GetProspectFilterOptionsQuery, ProspectFilterOptionsDto>
{
    public async ValueTask<ProspectFilterOptionsDto> Handle(GetProspectFilterOptionsQuery q, CancellationToken ct) => await queries.FilterOptionsAsync(ct);
}

// ---------- Acao em lote: mudar etapa / agendar contato ----------

/// <param name="NextFollowUpOn">Nulo = mantem a data de cada um.</param>
public sealed record BulkMoveProspectsCommand(IReadOnlyList<Guid> Ids, ProspectStage Stage, DateOnly? NextFollowUpOn) : ICommand<Result<int>>;

internal sealed class BulkMoveProspectsValidator : AbstractValidator<BulkMoveProspectsCommand>
{
    public BulkMoveProspectsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 1000).WithMessage("Selecione até 1000 prospects por vez.");
        RuleFor(x => x.Stage).IsInEnum();
    }
}

public sealed class BulkMoveProspectsHandler(IProspectRepository prospects, IUnitOfWork uow) : ICommandHandler<BulkMoveProspectsCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(BulkMoveProspectsCommand c, CancellationToken ct)
    {
        var list = await prospects.ListByIdsAsync(c.Ids.Distinct().ToList(), ct);
        foreach (var p in list)
        {
            p.MoveTo(c.Stage, c.NextFollowUpOn ?? p.NextFollowUpOn);
        }

        await uow.SaveChangesAsync(ct);
        return list.Count;
    }
}

// ---------- Excluir importacao (desfazer) ----------

/// <param name="Removed">Prospects que ainda estavam em "Novo" e sairam junto.</param>
/// <param name="Kept">Prospects ja trabalhados (avancaram no funil ou viraram cliente): ficam, sem vinculo com o lote.</param>
public sealed record DeleteProspectImportResultDto(int Removed, int Kept);

public sealed record DeleteProspectImportCommand(Guid Id) : ICommand<Result<DeleteProspectImportResultDto>>;

public sealed class DeleteProspectImportHandler(IProspectImportRepository imports, IProspectRepository prospects, IUnitOfWork uow)
    : ICommandHandler<DeleteProspectImportCommand, Result<DeleteProspectImportResultDto>>
{
    public async ValueTask<Result<DeleteProspectImportResultDto>> Handle(DeleteProspectImportCommand c, CancellationToken ct)
    {
        var import = await imports.GetByIdAsync(c.Id, ct);
        if (import is null)
        {
            return Error.NotFound("ProspectImport.NotFound", "Importação não encontrada.");
        }

        int removed = 0, kept = 0;
        foreach (var p in await prospects.ListByImportAsync(c.Id, ct))
        {
            if (p.IsUntouchedLead)
            {
                prospects.Remove(p);
                removed++;
            }
            else
            {
                p.DetachImport();
                kept++;
            }
        }

        imports.Remove(import);
        await uow.SaveChangesAsync(ct);
        return new DeleteProspectImportResultDto(removed, kept);
    }
}

/// <summary>
/// Filtro, ordenacao e numeros do dashboard, em memoria. Assim a regra e a mesma no SQLite (dev) e no Postgres, e
/// funciona com decimais (o SQLite nao compara decimal no SQL). Para o porte de uma corretora (milhares de linhas) e rapido.
/// </summary>
public static class ProspectFiltering
{
    public static IEnumerable<Prospect> Apply(IEnumerable<Prospect> source, ProspectFilter f, DateOnly today)
    {
        var term = string.IsNullOrWhiteSpace(f.Search) ? null : TextNormalizer.ForSearch(f.Search);
        var digits = new string((f.Search ?? string.Empty).Where(char.IsDigit).ToArray());
        var city = Norm(f.City);
        var state = Norm(f.State);
        var sourceName = Norm(f.Source);

        foreach (var p in source)
        {
            if (f.Stages is { Count: > 0 } stages && !stages.Contains(p.Stage)) continue;
            if (city is not null && Norm(p.City) != city) continue;
            if (state is not null && Norm(p.State) != state) continue;
            if (sourceName is not null && Norm(p.Source) != sourceName) continue;
            if (f.ImportId is { } importId && p.ImportId != importId) continue;
            if (f.SalaryMin is { } min && !(p.EstimatedSalary >= min)) continue;
            if (f.SalaryMax is { } max && !(p.EstimatedSalary <= max)) continue;
            var anyPhone = !string.IsNullOrWhiteSpace(p.Phone) || !string.IsNullOrWhiteSpace(p.Phone2);
            if (f.HasPhone is { } wantsPhone && wantsPhone != anyPhone) continue;
            if (!MatchesFollowUp(p, f.FollowUp, today)) continue;
            if (term is not null && !MatchesSearch(p, term, digits)) continue;
            yield return p;
        }
    }

    public static IEnumerable<Prospect> Sort(IEnumerable<Prospect> source, ProspectSort sort) => sort switch
    {
        ProspectSort.SalaryDesc => source.OrderBy(p => p.EstimatedSalary is null).ThenByDescending(p => p.EstimatedSalary).ThenBy(p => p.Name),
        ProspectSort.SalaryAsc => source.OrderBy(p => p.EstimatedSalary is null).ThenBy(p => p.EstimatedSalary).ThenBy(p => p.Name),
        ProspectSort.Name => source.OrderBy(p => TextNormalizer.ForSearch(p.Name)),
        ProspectSort.Newest => source.OrderByDescending(p => p.CreatedAt),
        ProspectSort.City => source.OrderBy(p => p.City is null).ThenBy(p => Norm(p.City)).ThenBy(p => TextNormalizer.ForSearch(p.Name)),
        _ => source.OrderBy(p => p.NextFollowUpOn ?? DateOnly.MaxValue).ThenByDescending(p => p.CreatedAt),
    };

    public static ProspectInsightsDto Insights(IReadOnlyCollection<Prospect> list, DateOnly today, DateTimeOffset now)
    {
        var open = list.Where(p => p.IsOpen).ToList();
        int won = list.Count(p => p.Stage == ProspectStage.Won), lost = list.Count(p => p.Stage == ProspectStage.Lost);
        var salaries = list.Where(p => p.EstimatedSalary is not null).Select(p => p.EstimatedSalary!.Value).ToList();

        static List<LabelCount> Top(IEnumerable<string?> values, int take) =>
            values.Select(v => string.IsNullOrWhiteSpace(v) ? "Não informado" : v.Trim())
                .GroupBy(v => TextNormalizer.ForSearch(v))
                .Select(g => new LabelCount(g.First(), g.Count()))
                .OrderByDescending(x => x.Count).ThenBy(x => x.Label).Take(take).ToList();

        return new ProspectInsightsDto(
            list.Count,
            open.Count,
            list.Count(p => p.CreatedAt >= now.AddDays(-7)),
            open.Count(p => p.NextFollowUpOn < today),
            open.Count(p => p.NextFollowUpOn == today),
            won,
            lost,
            won + lost > 0 ? Math.Round(won * 100m / (won + lost), 1) : null,
            salaries.Count > 0 ? Math.Round(salaries.Average(), 2) : null,
            salaries.Count,
            Enum.GetValues<ProspectStage>().Select(s => new LabelCount(s.ToString(), list.Count(p => p.Stage == s))).ToList(),
            Top(list.Select(p => p.City), 8),
            Top(list.Select(p => p.Source), 6),
            SalaryBands(list));
    }

    private static List<LabelCount> SalaryBands(IEnumerable<Prospect> list)
    {
        (string Label, decimal? Max)[] bands = [("Até R$ 3 mil", 3000m), ("R$ 3 a 5 mil", 5000m), ("R$ 5 a 8 mil", 8000m), ("R$ 8 a 12 mil", 12000m), ("Acima de R$ 12 mil", null)];
        var counts = new int[bands.Length + 1];
        foreach (var p in list)
        {
            if (p.EstimatedSalary is not { } s)
            {
                counts[^1]++;
                continue;
            }

            var i = Array.FindIndex(bands, b => b.Max is null || s <= b.Max);
            counts[i]++;
        }

        return [.. bands.Select((b, i) => new LabelCount(b.Label, counts[i])), new LabelCount("Sem salário", counts[^1])];
    }

    private static bool MatchesFollowUp(Prospect p, FollowUpFilter f, DateOnly today) => f switch
    {
        FollowUpFilter.Overdue => p.IsOpen && p.NextFollowUpOn < today,
        FollowUpFilter.Today => p.IsOpen && p.NextFollowUpOn == today,
        FollowUpFilter.Next7Days => p.IsOpen && p.NextFollowUpOn >= today && p.NextFollowUpOn <= today.AddDays(7),
        FollowUpFilter.None => p.IsOpen && p.NextFollowUpOn is null,
        _ => true,
    };

    private static bool MatchesSearch(Prospect p, string term, string digits) =>
        TextNormalizer.ForSearch(p.Name).Contains(term)
        || (p.Email ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
        || (p.Occupation is { } o && TextNormalizer.ForSearch(o).Contains(term))
        || (p.Address is { } a && TextNormalizer.ForSearch(a).Contains(term))
        || (digits.Length >= 4 && (Digits(p.Phone).Contains(digits) || Digits(p.Phone2).Contains(digits)));

    private static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) ? null : TextNormalizer.ForSearch(value);
}
