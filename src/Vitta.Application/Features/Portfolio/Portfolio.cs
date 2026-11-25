using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Features.Imports;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;
using Vitta.Domain.Portfolio;
using F = Vitta.Domain.Imports.ImportFields.Portfolio;

namespace Vitta.Application.Features.Portfolio;

public record PortfolioImportDto(
    Guid Id, Guid InsurerId, string InsurerName, DateOnly ReferenceMonth, string FileName, PortfolioImportStatus Status,
    int TotalRows, int Entered, int Left, int Changed, bool IsFirstImport, DateTimeOffset CreatedAt);

public sealed record PortfolioImportDetailDto(
    Guid Id, Guid InsurerId, string InsurerName, DateOnly ReferenceMonth, string FileName, PortfolioImportStatus Status,
    int TotalRows, int Entered, int Left, int Changed, bool IsFirstImport, DateTimeOffset CreatedAt,
    IReadOnlyList<PortfolioChangeDto> Changes, ImportTotalsDto Totals)
    : PortfolioImportDto(Id, InsurerId, InsurerName, ReferenceMonth, FileName, Status, TotalRows, Entered, Left, Changed, IsFirstImport, CreatedAt);

/// <summary>Totais do mes importado e do mes anterior da mesma seguradora (nulo na primeira importacao).</summary>
public sealed record ImportTotalsDto(MonthTotalsDto Current, MonthTotalsDto? Previous);

/// <param name="Commission">Soma da comissao (coluna de comissao ou premio x %); nulo quando a planilha nao traz nenhuma das duas.</param>
/// <param name="AverageRate">Comissao / premio das linhas com comissao, em %.</param>
public sealed record MonthTotalsDto(DateOnly Month, int Policies, decimal Premium, decimal? Commission, decimal? AverageRate)
{
    public static MonthTotalsDto From(DateOnly month, IReadOnlyCollection<PortfolioRow> rows)
    {
        var commissions = rows
            .Select(r => (r.MonthlyPremium, Commission: r.CommissionAmount ?? Domain.Policies.Policy.CommissionOf(r.MonthlyPremium ?? 0, PolicyCarrierSync.RateOf(r))))
            .Where(x => x.Commission is not null)
            .ToList();
        decimal? commission = commissions.Count > 0 ? commissions.Sum(x => x.Commission!.Value) : null;
        var premiumWithCommission = commissions.Sum(x => x.MonthlyPremium ?? 0);
        return new MonthTotalsDto(month, rows.Count, rows.Sum(r => r.MonthlyPremium ?? 0), commission,
            commission is { } c && premiumWithCommission > 0 ? Math.Round(c / premiumWithCommission * 100m, 2) : null);
    }
}

public sealed record PortfolioChangeDto(
    Guid Id, Guid ImportId, DateOnly ReferenceMonth, string InsurerName, PortfolioChangeType Type, string PolicyNumber,
    string HolderName, string? HolderCpf, string? Detail, Guid? ClientId);

/// <summary>Linha da carteira do mes (a "tabela final" da importacao).</summary>
public sealed record PortfolioRowDto(
    Guid Id, string DisplayId, string? PolicyNumber, string HolderName, string? HolderCpf, string? ProductName, decimal? MonthlyPremium,
    string? Status, DateOnly? StartDate, string? City, DateOnly? Competence, int? Installment, decimal? CommissionAmount,
    decimal? CommissionRate, int Coverages, string? Extras, Guid? ClientId);

public sealed record GetPortfolioImportRowsQuery(Guid Id) : IQuery<Result<IReadOnlyList<PortfolioRowDto>>>;

public sealed class GetPortfolioImportRowsHandler(IPortfolioQueries queries) : IQueryHandler<GetPortfolioImportRowsQuery, Result<IReadOnlyList<PortfolioRowDto>>>
{
    public async ValueTask<Result<IReadOnlyList<PortfolioRowDto>>> Handle(GetPortfolioImportRowsQuery q, CancellationToken ct) =>
        await queries.ListRowsAsync(q.Id, ct) is { } rows ? Result.Success(rows) : PortfolioErrors.NotFound(q.Id);
}

// ---- Listar / detalhar ----
public sealed record GetPortfolioImportsQuery : IQuery<IReadOnlyList<PortfolioImportDto>>;

public sealed class GetPortfolioImportsHandler(IPortfolioQueries queries) : IQueryHandler<GetPortfolioImportsQuery, IReadOnlyList<PortfolioImportDto>>
{
    public async ValueTask<IReadOnlyList<PortfolioImportDto>> Handle(GetPortfolioImportsQuery q, CancellationToken ct) =>
        await queries.ListImportsAsync(ct);
}

public sealed record GetPortfolioImportQuery(Guid Id) : IQuery<Result<PortfolioImportDetailDto>>;

public sealed class GetPortfolioImportHandler(IPortfolioQueries queries) : IQueryHandler<GetPortfolioImportQuery, Result<PortfolioImportDetailDto>>
{
    public async ValueTask<Result<PortfolioImportDetailDto>> Handle(GetPortfolioImportQuery q, CancellationToken ct) =>
        await queries.GetImportAsync(q.Id, ct) is { } dto ? dto : PortfolioErrors.NotFound(q.Id);
}

// ---- Importar planilha ----
/// <param name="InsurerId">Pode ser omitido quando o modelo ja define a seguradora.</param>
/// <param name="TemplateId">Modelo de planilha; sem modelo, as colunas sao detectadas pelos nomes conhecidos.</param>
public sealed record ImportPortfolioCommand(Guid? InsurerId, DateOnly ReferenceMonth, string FileName, Stream File, Guid? TemplateId = null)
    : ICommand<Result<PortfolioImportDetailDto>>;

public sealed class ImportPortfolioHandler(
    IInsurerRepository insurers,
    IPortfolioImportRepository imports,
    IImportTemplateRepository templates,
    IPolicyRepository policies,
    ISpreadsheetReader reader,
    IPortfolioQueries queries,
    TimeProvider clock,
    IUnitOfWork uow) : ICommandHandler<ImportPortfolioCommand, Result<PortfolioImportDetailDto>>
{
    public async ValueTask<Result<PortfolioImportDetailDto>> Handle(ImportPortfolioCommand c, CancellationToken ct)
    {
        ImportTemplate? template = null;
        if (c.TemplateId is { } templateId)
        {
            template = await templates.GetByIdAsync(templateId, ct);
            if (template is null)
            {
                return ImportTemplateErrors.NotFound(templateId);
            }

            if (template.Kind != ImportTemplateKind.Portfolio)
            {
                return ImportTemplateErrors.WrongKind;
            }
        }

        var insurerId = c.InsurerId ?? template?.InsurerId;
        if (insurerId is null)
        {
            return PortfolioErrors.InsurerRequired;
        }

        if (await insurers.GetByIdAsync(insurerId.Value, ct) is null)
        {
            return Insurer.NotFound(insurerId.Value);
        }

        var month = PortfolioImport.FirstDayOf(c.ReferenceMonth);
        if (await imports.ExistsAsync(insurerId.Value, month, ct))
        {
            return PortfolioErrors.MonthAlreadyImported;
        }

        var data = await reader.ReadAsync(c.File, c.FileName, template?.SheetName, ct);
        if (data.IsFailure)
        {
            return data.Error;
        }

        var rows = template is not null ? ReadWithTemplate(data.Value, template) : ReadAutomatically(data.Value);
        if (rows.IsFailure)
        {
            return rows.Error;
        }

        if (rows.Value.Count == 0)
        {
            return PortfolioErrors.EmptyFile;
        }

        // O usuario escolhe o mes; se o arquivo diz outra competencia, avisa antes de gravar (evita importar outubro como novembro).
        if (FileCompetence(rows.Value) is { } fileMonth && fileMonth != month)
        {
            return PortfolioErrors.CompetenceMismatch(fileMonth, month);
        }

        var previous = await imports.GetPreviousWithRowsAsync(insurerId.Value, month, ct);
        var import = PortfolioImport.Create(insurerId.Value, month, Path.GetFileName(c.FileName));
        import.Process(rows.Value, previous);
        template?.MarkUsed(clock.GetUtcNow());
        // Mantem premio e % de comissao das apolices cadastradas iguais ao que a seguradora informou.
        PolicyCarrierSync.Apply(rows.Value, await policies.ListForCarrierSyncAsync(insurerId.Value, ct));

        imports.Add(import);
        await uow.SaveChangesAsync(ct);

        return (await queries.GetImportAsync(import.Id, ct))!;
    }

    private static Result<IReadOnlyList<PortfolioRow>> ReadWithTemplate(SpreadsheetData data, ImportTemplate template)
    {
        var mapped = SpreadsheetMapper.Apply(data, template.HeaderRow, template.Columns, template.Name);
        return mapped.IsFailure ? mapped.Error : Result.Success(ToPortfolioRows(mapped.Value));
    }

    /// <summary>Sem modelo: acha o cabecalho e as colunas pelos nomes conhecidos (Apólice, Segurado, CPF...).</summary>
    private static Result<IReadOnlyList<PortfolioRow>> ReadAutomatically(SpreadsheetData data)
    {
        if (data.Rows.Count == 0)
        {
            return PortfolioErrors.EmptyFile;
        }

        var headerRow = SpreadsheetMapper.DetectHeaderRow(data.Rows, ImportTemplateKind.Portfolio);
        var columns = headerRow == 0
            ? SpreadsheetMapper.AutoMapByContent(data.Rows.Take(200).ToList(), ImportTemplateKind.Portfolio)
            : SpreadsheetMapper.AutoMap(SpreadsheetMapper.HeadersAt(data, headerRow), ImportTemplateKind.Portfolio);
        if (ImportFields.MissingRequired(ImportTemplateKind.Portfolio, columns.Select(m => m.Field)).Count > 0)
        {
            return PortfolioErrors.InvalidFile(
                "Não encontrei as colunas de apólice (ou CPF) e de segurado. Crie um modelo para esta planilha (a IA ajuda a entender o layout).");
        }

        var mapped = SpreadsheetMapper.Apply(data, headerRow, columns, "automático");
        return mapped.IsFailure ? mapped.Error : Result.Success(ToPortfolioRows(mapped.Value));
    }

    /// <summary>
    /// Traduz as linhas mapeadas para a carteira. Linhas da mesma apolice (ex.: extrato de comissoes da MAG, uma linha
    /// por cobertura) viram uma so: premio e comissao somados, produtos juntados. Sem nº da apolice, a linha e
    /// identificada pelo CPF + inicio da vigencia.
    /// </summary>
    public static IReadOnlyList<PortfolioRow> ToPortfolioRows(IEnumerable<MappedRow> rows)
    {
        var parsed = new List<(string Key, PortfolioRow Row)>();
        foreach (var row in rows)
        {
            var cpfDigits = SpreadsheetMapper.Digits(row.Get(F.HolderCpf));
            var cpf = cpfDigits.Length is > 0 and <= 11 ? cpfDigits.PadLeft(11, '0') : null;
            var policy = row.Get(F.PolicyNumber);
            var startDate = SpreadsheetMapper.ParseDate(row.Get(F.StartDate));
            var key = policy is not null ? "P:" + policy.Trim().TrimStart('0').ToUpperInvariant()
                : cpf is not null ? $"C:{cpf}|{startDate:yyyyMMdd}" : null;
            if (key is null)
            {
                continue;
            }

            var extras = string.Join('\n', row.All(F.Extras).Select(e => $"{e.Column}: {e.Value}"));
            parsed.Add((key, new PortfolioRow(
                policy?.Trim(),
                row.Get(F.HolderName) ?? "(sem nome)",
                cpf,
                row.Get(F.ProductName),
                SpreadsheetMapper.ParseMoney(row.Get(F.MonthlyPremium)),
                row.Get(F.Status))
            {
                StartDate = startDate,
                City = Limit(row.Get(F.City), 100),
                Competence = ParseCompetence(row.Get(F.Competence)),
                Installment = int.TryParse(SpreadsheetMapper.Digits(row.Get(F.Installment)), out var n) ? n : null,
                CommissionAmount = SpreadsheetMapper.ParseMoney(row.Get(F.CommissionAmount)),
                CommissionRate = SpreadsheetMapper.ParseMoney(row.Get(F.CommissionRate)),
                Extras = extras.Length == 0 ? null : extras,
            }));
        }

        return parsed.GroupBy(p => p.Key).Select(g => Merge(g.Select(p => p.Row).ToList())).ToList();
    }

    private static PortfolioRow Merge(List<PortfolioRow> rows)
    {
        if (rows.Count == 1)
        {
            return rows[0];
        }

        static decimal? Sum(IEnumerable<decimal?> values) => values.Any(v => v is not null) ? values.Sum(v => v ?? 0) : null;
        var first = rows[0];
        var products = rows.Select(r => r.ProductName?.Trim()).OfType<string>().Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var extras = string.Join('\n', rows.Select(r => r.Extras).OfType<string>().SelectMany(e => e.Split('\n')).Distinct());
        return first with
        {
            ProductName = products.Count == 0 ? null : Limit(string.Join(" + ", products), 300),
            MonthlyPremium = Sum(rows.Select(r => r.MonthlyPremium)),
            Status = rows.Select(r => r.Status).OfType<string>().FirstOrDefault(),
            StartDate = rows.Select(r => r.StartDate).OfType<DateOnly>().DefaultIfEmpty().Min() is var d && d != default ? d : null,
            Installment = rows.Max(r => r.Installment),
            CommissionAmount = Sum(rows.Select(r => r.CommissionAmount)),
            CommissionRate = rows.Select(r => r.CommissionRate).Distinct().Count() == 1 ? first.CommissionRate : null,
            Coverages = rows.Sum(r => r.Coverages),
            Extras = extras.Length == 0 ? null : Limit(extras, 2000),
        };
    }

    /// <summary>Competencia em "202610", "10/2026", "2026-10", "10/2026" ou data completa.</summary>
    public static DateOnly? ParseCompetence(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var digits = SpreadsheetMapper.Digits(text);
        if (digits.Length == 6 && digits == text)
        {
            return MonthOf(int.Parse(digits[..4]), int.Parse(digits[4..]));
        }

        var parts = text.Split('/', '-', '.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b))
        {
            return a > 12 ? MonthOf(a, b) : MonthOf(b, a);
        }

        return SpreadsheetMapper.ParseDate(text) is { } date ? new DateOnly(date.Year, date.Month, 1) : null;
    }

    private static DateOnly? MonthOf(int year, int month) =>
        year is >= 2000 and <= 2100 && month is >= 1 and <= 12 ? new DateOnly(year, month, 1) : null;

    /// <summary>Competencia predominante no arquivo (quando a coluna foi mapeada).</summary>
    internal static DateOnly? FileCompetence(IReadOnlyList<PortfolioRow> rows)
    {
        var values = rows.Select(r => r.Competence).OfType<DateOnly>().ToList();
        if (values.Count == 0)
        {
            return null;
        }

        var top = values.GroupBy(v => v).OrderByDescending(g => g.Count()).First();
        return top.Count() >= values.Count * 0.8 ? top.Key : null;
    }

    private static string? Limit(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();
}

// ---- Excluir ----
public sealed record DeletePortfolioImportCommand(Guid Id) : ICommand<Result>;

public sealed class DeletePortfolioImportHandler(IPortfolioImportRepository imports, IUnitOfWork uow) : ICommandHandler<DeletePortfolioImportCommand, Result>
{
    public async ValueTask<Result> Handle(DeletePortfolioImportCommand c, CancellationToken ct)
    {
        var import = await imports.GetByIdAsync(c.Id, ct);
        if (import is null)
        {
            return PortfolioErrors.NotFound(c.Id);
        }

        imports.Remove(import);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
