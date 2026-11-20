using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Features.Imports;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;
using Vitta.Domain.Portfolio;

namespace Vitta.Application.Features.Portfolio;

public record PortfolioImportDto(
    Guid Id, Guid InsurerId, string InsurerName, DateOnly ReferenceMonth, string FileName, PortfolioImportStatus Status,
    int TotalRows, int Entered, int Left, int Changed, bool IsFirstImport, DateTimeOffset CreatedAt);

public sealed record PortfolioImportDetailDto(
    Guid Id, Guid InsurerId, string InsurerName, DateOnly ReferenceMonth, string FileName, PortfolioImportStatus Status,
    int TotalRows, int Entered, int Left, int Changed, bool IsFirstImport, DateTimeOffset CreatedAt,
    IReadOnlyList<PortfolioChangeDto> Changes)
    : PortfolioImportDto(Id, InsurerId, InsurerName, ReferenceMonth, FileName, Status, TotalRows, Entered, Left, Changed, IsFirstImport, CreatedAt);

public sealed record PortfolioChangeDto(
    Guid Id, Guid ImportId, DateOnly ReferenceMonth, string InsurerName, PortfolioChangeType Type, string PolicyNumber,
    string HolderName, string? HolderCpf, string? Detail, Guid? ClientId);

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

        var previous = await imports.GetPreviousWithRowsAsync(insurerId.Value, month, ct);
        var import = PortfolioImport.Create(insurerId.Value, month, Path.GetFileName(c.FileName));
        import.Process(rows.Value, previous);
        template?.MarkUsed(clock.GetUtcNow());

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

    public static IReadOnlyList<PortfolioRow> ToPortfolioRows(IEnumerable<MappedRow> rows)
    {
        var result = new List<PortfolioRow>();
        foreach (var row in rows)
        {
            var cpfDigits = SpreadsheetMapper.Digits(row.Get(ImportFields.Portfolio.HolderCpf));
            var policy = row.Get(ImportFields.Portfolio.PolicyNumber) ?? KeyWithoutPolicy(cpfDigits, row.Get(ImportFields.Portfolio.StartDate));
            if (policy is null)
            {
                continue;
            }

            result.Add(new PortfolioRow(
                policy,
                row.Get(ImportFields.Portfolio.HolderName) ?? "(sem nome)",
                cpfDigits.Length is > 0 and <= 11 ? cpfDigits.PadLeft(11, '0') : null,
                row.Get(ImportFields.Portfolio.ProductName),
                SpreadsheetMapper.ParseMoney(row.Get(ImportFields.Portfolio.MonthlyPremium)),
                row.Get(ImportFields.Portfolio.Status)));
        }

        return result;
    }

    /// <summary>
    /// Sem nº da apolice (ex.: tabela copiada do site da MAG), a linha e identificada pelo CPF e, se houver, pelo inicio
    /// da vigencia (diferencia duas apolices do mesmo cliente). Ex.: "CPF 026.078.677-20 · 22/05/2017".
    /// </summary>
    internal static string? KeyWithoutPolicy(string cpfDigits, string? startDate)
    {
        if (cpfDigits.Length is 0 or > 11)
        {
            return null;
        }

        var cpf = cpfDigits.PadLeft(11, '0');
        var key = $"CPF {cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
        var date = SpreadsheetMapper.ParseDate(startDate);
        return date is { } d ? $"{key} · {d:dd/MM/yyyy}" : key;
    }
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
