using Mediator;
using Microsoft.Extensions.Logging;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;

namespace Vitta.Application.Features.Imports;

public sealed record ImportTemplateDto(
    Guid Id, string Name, ImportTemplateKind Kind, Guid? InsurerId, string? InsurerName, string? SheetName, int HeaderRow,
    IReadOnlyList<ColumnMapping> Columns, bool CreatedByAi, string? AiNotes, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

public sealed record ImportFieldDto(string Field, string Label, bool Required, bool Multiple, string Description)
{
    public static ImportFieldDto From(ImportField f) => new(f.Key, f.Label, f.Required, f.Multiple, f.Description);
}

public sealed record SpreadsheetAnalysisDto(
    string FileName, IReadOnlyList<string> Sheets, string? SheetName, IReadOnlyList<string[]> Rows, int HeaderRow,
    IReadOnlyList<ColumnMapping> Columns, string SuggestedName, string? Notes, string Confidence, string AnalyzedBy);

// ---- Consultas ----
public sealed record GetImportTemplatesQuery(ImportTemplateKind? Kind) : IQuery<IReadOnlyList<ImportTemplateDto>>;

public sealed class GetImportTemplatesHandler(IImportTemplateQueries queries) : IQueryHandler<GetImportTemplatesQuery, IReadOnlyList<ImportTemplateDto>>
{
    public async ValueTask<IReadOnlyList<ImportTemplateDto>> Handle(GetImportTemplatesQuery q, CancellationToken ct) =>
        await queries.ListAsync(q.Kind, ct);
}

public sealed record GetImportTemplateQuery(Guid Id) : IQuery<Result<ImportTemplateDto>>;

public sealed class GetImportTemplateHandler(IImportTemplateQueries queries) : IQueryHandler<GetImportTemplateQuery, Result<ImportTemplateDto>>
{
    public async ValueTask<Result<ImportTemplateDto>> Handle(GetImportTemplateQuery q, CancellationToken ct) =>
        await queries.GetAsync(q.Id, ct) is { } dto ? dto : ImportTemplateErrors.NotFound(q.Id);
}

public sealed record GetImportFieldsQuery(ImportTemplateKind Kind) : IQuery<IReadOnlyList<ImportFieldDto>>;

public sealed class GetImportFieldsHandler : IQueryHandler<GetImportFieldsQuery, IReadOnlyList<ImportFieldDto>>
{
    public ValueTask<IReadOnlyList<ImportFieldDto>> Handle(GetImportFieldsQuery q, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<ImportFieldDto>>(ImportFields.For(q.Kind).Select(ImportFieldDto.From).ToList());
}

// ---- Criar / alterar / excluir ----
public sealed record SaveImportTemplateCommand(
    Guid? Id, string Name, ImportTemplateKind Kind, Guid? InsurerId, string? SheetName, int HeaderRow,
    IReadOnlyList<ColumnMapping> Columns, bool CreatedByAi, string? AiNotes) : ICommand<Result<Guid>>;

public sealed class SaveImportTemplateHandler(IImportTemplateRepository templates, IInsurerRepository insurers, IUnitOfWork uow)
    : ICommandHandler<SaveImportTemplateCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(SaveImportTemplateCommand c, CancellationToken ct)
    {
        if (c.Kind == ImportTemplateKind.Portfolio && c.InsurerId is { } insurerId && await insurers.GetByIdAsync(insurerId, ct) is null)
        {
            return Insurer.NotFound(insurerId);
        }

        if (await templates.NameExistsAsync(c.Kind, c.Name ?? string.Empty, c.Id, ct))
        {
            return ImportTemplateErrors.NameAlreadyExists;
        }

        var columns = c.Columns ?? [];
        if (c.Id is { } id)
        {
            var template = await templates.GetByIdAsync(id, ct);
            if (template is null)
            {
                return ImportTemplateErrors.NotFound(id);
            }

            if (template.Kind != c.Kind)
            {
                return ImportTemplateErrors.WrongKind;
            }

            var updated = template.Update(c.Name ?? string.Empty, c.InsurerId, c.SheetName, c.HeaderRow, columns, c.AiNotes);
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            await uow.SaveChangesAsync(ct);
            return template.Id;
        }

        var created = ImportTemplate.Create(c.Name ?? string.Empty, c.Kind, c.InsurerId, c.SheetName, c.HeaderRow, columns, c.CreatedByAi, c.AiNotes);
        if (created.IsFailure)
        {
            return created.Error;
        }

        templates.Add(created.Value);
        await uow.SaveChangesAsync(ct);
        return created.Value.Id;
    }
}

public sealed record DeleteImportTemplateCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteImportTemplateHandler(IImportTemplateRepository templates, IUnitOfWork uow) : ICommandHandler<DeleteImportTemplateCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteImportTemplateCommand c, CancellationToken ct)
    {
        var template = await templates.GetByIdAsync(c.Id, ct);
        if (template is null)
        {
            return ImportTemplateErrors.NotFound(c.Id);
        }

        templates.Remove(template);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Analisar planilha de exemplo (IA) ----
public sealed record AnalyzeSpreadsheetCommand(ImportTemplateKind Kind, string FileName, Stream File, string? SheetName)
    : ICommand<Result<SpreadsheetAnalysisDto>>;

public sealed class AnalyzeSpreadsheetHandler(ISpreadsheetReader reader, IAiService ai, ILogger<AnalyzeSpreadsheetHandler> logger)
    : ICommandHandler<AnalyzeSpreadsheetCommand, Result<SpreadsheetAnalysisDto>>
{
    /// <summary>Linhas enviadas para a IA e devolvidas para a pre-visualizacao.</summary>
    internal const int PreviewRows = 25;
    private const int MaxColumns = 60;
    private const int MaxCellLength = 120;

    public async ValueTask<Result<SpreadsheetAnalysisDto>> Handle(AnalyzeSpreadsheetCommand c, CancellationToken ct)
    {
        var read = await reader.ReadAsync(c.File, c.FileName, c.SheetName, ct);
        if (read.IsFailure)
        {
            return read.Error;
        }

        var data = read.Value;
        var preview = data.Rows.Take(PreviewRows)
            .Select(r => r.Take(MaxColumns).Select(v => v.Length > MaxCellLength ? v[..MaxCellLength] : v).ToArray())
            .ToList();
        if (preview.Count == 0 || preview.All(r => r.All(string.IsNullOrWhiteSpace)))
        {
            return SpreadsheetErrors.EmptyFile;
        }

        var fields = ImportFields.For(c.Kind);
        SpreadsheetSuggestion suggestion;
        try
        {
            var masked = preview.Select(r => r.Select(MaskForAi).ToArray()).ToList();
            suggestion = await ai.AnalyzeSpreadsheetAsync(new SpreadsheetAnalysisRequest(c.Kind, c.FileName, data.SheetName, masked, fields), ct);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning(ex, "Camada de IA indisponivel; usando heuristica para {FileName}", c.FileName);
            suggestion = Heuristic(c.Kind, preview);
        }

        var headerRow = Math.Clamp(suggestion.HeaderRow, 0, preview.Count);
        var headers = SpreadsheetMapper.HeadersAt(preview, headerRow);
        var columns = Sanitize(suggestion.Columns, headers, fields);
        if (columns.Count == 0 && suggestion.AnalyzedBy == "ai")
        {
            columns = headerRow == 0 ? SpreadsheetMapper.AutoMapByContent(preview, c.Kind) : SpreadsheetMapper.AutoMap(headers, c.Kind);
        }

        var baseName = Path.GetFileNameWithoutExtension(c.FileName);
        var suggestedName = string.IsNullOrWhiteSpace(suggestion.SuggestedName)
            ? (c.Kind == ImportTemplateKind.Portfolio ? "Carteira – " : "Prospects – ") + (IsPasted(c.FileName) ? "site" : baseName)
            : suggestion.SuggestedName.Trim();

        return new SpreadsheetAnalysisDto(
            Path.GetFileName(c.FileName), data.Sheets, data.SheetName, preview, headerRow, columns, suggestedName,
            suggestion.Notes, suggestion.Confidence is "high" or "medium" or "low" ? suggestion.Confidence : "low", suggestion.AnalyzedBy);
    }

    private static bool IsPasted(string fileName) => fileName.StartsWith("texto-colado", StringComparison.OrdinalIgnoreCase);

    internal static SpreadsheetSuggestion Heuristic(ImportTemplateKind kind, IReadOnlyList<string[]> rows)
    {
        var headerRow = SpreadsheetMapper.DetectHeaderRow(rows, kind);
        var headers = SpreadsheetMapper.HeadersAt(rows, headerRow);
        var mapped = headerRow == 0 ? SpreadsheetMapper.AutoMapByContent(rows, kind) : SpreadsheetMapper.AutoMap(headers, kind);
        var columns = mapped
            .Select(m => new SuggestedColumn(m.Field, Array.FindIndex(headers, h => h.Trim() == m.Column) + 1))
            .ToList();
        var requiredOk = ImportFields.MissingRequired(kind, columns.Select(m => m.Field)).Count == 0;
        var notes = headerRow == 0
            ? "Texto sem linha de títulos: as colunas foram reconhecidas pelo conteúdo (IA indisponível). Confira antes de salvar."
            : "Sugestão feita pelos nomes das colunas (IA indisponível). Confira antes de salvar.";
        return new SpreadsheetSuggestion(headerRow, columns, null, notes, requiredOk ? "medium" : "low", "heuristic");
    }

    /// <summary>Mantem so campos validos e colunas preenchidas na linha do cabecalho, com o texto original do cabecalho.</summary>
    private static List<ColumnMapping> Sanitize(IReadOnlyList<SuggestedColumn> suggested, string[] headers, IReadOnlyList<ImportField> fields)
    {
        var result = new List<ColumnMapping>();
        foreach (var column in suggested)
        {
            var field = fields.FirstOrDefault(f => f.Key == column.Field);
            var index = column.ColumnNumber - 1;
            if (field is null || index < 0 || index >= headers.Length || string.IsNullOrWhiteSpace(headers[index])
                || (!field.Multiple && result.Any(r => r.Field == field.Key))
                || result.Any(r => r.Column == headers[index]))
            {
                continue;
            }

            result.Add(new ColumnMapping(field.Key, headers[index]));
        }

        return result;
    }

    /// <summary>
    /// Protege dados pessoais antes de enviar para a IA: digitos viram 9 (CPF, telefone e valores mantem so o formato)
    /// e e-mails sao trocados por um exemplo. Cabecalhos continuam legiveis.
    /// </summary>
    internal static string MaskForAi(string value)
    {
        if (value.Contains('@'))
        {
            return "email@exemplo.com";
        }

        return value.Any(char.IsDigit) ? new string(value.Select(ch => char.IsDigit(ch) ? '9' : ch).ToArray()) : value;
    }
}
