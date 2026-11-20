using Vitta.Domain.Common;

namespace Vitta.Domain.Imports;

/// <summary>Liga um campo do Vitta a uma coluna da planilha (pelo texto do cabecalho, ou "Coluna N" quando nao ha cabecalho).</summary>
public sealed record ColumnMapping(string Field, string Column);

/// <summary>
/// Modelo de planilha: guarda o "entendimento" do layout de uma planilha recorrente (aba, linha do cabecalho e
/// qual coluna alimenta cada campo). Normalmente nasce de uma sugestao da camada de IA, conferida pelo usuario.
/// </summary>
public sealed class ImportTemplate : AggregateRoot
{
    private readonly List<ColumnMapping> _columns = [];

    private ImportTemplate() { } // EF

    public string Name { get; private set; } = null!;
    public ImportTemplateKind Kind { get; private set; }
    /// <summary>Seguradora a que o modelo se refere (so para carteira; opcional).</summary>
    public Guid? InsurerId { get; private set; }
    /// <summary>Aba do .xlsx; nulo = primeira aba (ou .csv).</summary>
    public string? SheetName { get; private set; }
    /// <summary>Linha do cabecalho, comecando em 1. Zero = sem cabecalho (texto colado do site): colunas pela posicao.</summary>
    public int HeaderRow { get; private set; }
    public IReadOnlyList<ColumnMapping> Columns => _columns;
    public bool CreatedByAi { get; private set; }
    public string? AiNotes { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public static Result<ImportTemplate> Create(
        string name, ImportTemplateKind kind, Guid? insurerId, string? sheetName, int headerRow,
        IEnumerable<ColumnMapping> columns, bool createdByAi, string? aiNotes)
    {
        var template = new ImportTemplate { Kind = kind, CreatedByAi = createdByAi };
        var result = template.Update(name, insurerId, sheetName, headerRow, columns, aiNotes);
        return result.IsFailure ? result.Error : template;
    }

    public Result Update(string name, Guid? insurerId, string? sheetName, int headerRow, IEnumerable<ColumnMapping> columns, string? aiNotes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ImportTemplateErrors.NameRequired;
        }

        if (headerRow is < 0 or > 1000)
        {
            return ImportTemplateErrors.InvalidHeaderRow;
        }

        var fields = ImportFields.For(Kind);
        var cleaned = new List<ColumnMapping>();
        foreach (var mapping in columns)
        {
            if (string.IsNullOrWhiteSpace(mapping.Column) || string.IsNullOrWhiteSpace(mapping.Field))
            {
                continue;
            }

            var field = fields.FirstOrDefault(f => f.Key == mapping.Field);
            if (field is null)
            {
                return ImportTemplateErrors.UnknownField(mapping.Field);
            }

            if (!field.Multiple && cleaned.Any(c => c.Field == field.Key))
            {
                return ImportTemplateErrors.DuplicateField(field.Label);
            }

            var candidate = new ColumnMapping(field.Key, mapping.Column.Trim());
            if (!cleaned.Contains(candidate))
            {
                cleaned.Add(candidate);
            }
        }

        var missing = ImportFields.MissingRequired(Kind, cleaned.Select(c => c.Field));
        if (missing.Count > 0)
        {
            return ImportTemplateErrors.RequiredFieldsMissing(missing);
        }

        Name = name.Trim();
        InsurerId = Kind == ImportTemplateKind.Portfolio ? insurerId : null;
        SheetName = string.IsNullOrWhiteSpace(sheetName) ? null : sheetName.Trim();
        HeaderRow = headerRow;
        AiNotes = string.IsNullOrWhiteSpace(aiNotes) ? null : aiNotes.Trim();
        _columns.Clear();
        _columns.AddRange(cleaned);
        Touch();
        return Result.Success();
    }

    public void MarkUsed(DateTimeOffset at) => LastUsedAt = at;
}

public static class ImportTemplateErrors
{
    public static readonly Error NameRequired = Error.Validation("ImportTemplate.NameRequired", "Dê um nome ao modelo.");
    public static readonly Error InvalidHeaderRow = Error.Validation("ImportTemplate.InvalidHeaderRow", "Linha do cabeçalho inválida.");
    public static readonly Error NameAlreadyExists = Error.Conflict("ImportTemplate.NameAlreadyExists", "Já existe um modelo com este nome.");
    public static readonly Error WrongKind = Error.Validation("ImportTemplate.WrongKind", "Este modelo é de outro tipo de planilha.");
    public static Error UnknownField(string field) => Error.Validation("ImportTemplate.UnknownField", $"Campo desconhecido: {field}.");
    public static Error DuplicateField(string label) => Error.Validation("ImportTemplate.DuplicateField", $"O campo \"{label}\" só pode ter uma coluna.");
    public static Error RequiredFieldsMissing(IEnumerable<string> labels) =>
        Error.Validation("ImportTemplate.RequiredFieldsMissing", $"Escolha a coluna de: {string.Join(", ", labels)}.");
    public static Error ColumnsNotFound(string templateName, IEnumerable<string> columns) =>
        Error.Validation("ImportTemplate.ColumnsNotFound",
            $"A planilha não tem a(s) coluna(s) {string.Join(", ", columns.Select(c => $"\"{c}\""))} do modelo \"{templateName}\". " +
            "O layout pode ter mudado: ajuste o modelo ou crie um novo.");
    public static Error LayoutChanged(string templateName, IEnumerable<string> columns) =>
        Error.Validation("ImportTemplate.LayoutChanged",
            $"Os dados não batem com o modelo \"{templateName}\": {string.Join("; ", columns)}. " +
            "A ordem das colunas pode ter mudado no site: confira se copiou a tabela inteira ou ajuste o modelo.");
    public static Error NotFound(Guid id) => Error.NotFound("ImportTemplate.NotFound", "Modelo de planilha não encontrado.");
}

public static class SpreadsheetErrors
{
    public static readonly Error EmptyFile = Error.Validation("Spreadsheet.EmptyFile", "A planilha está vazia.");
    public static Error InvalidFile(string message) => Error.Validation("Spreadsheet.InvalidFile", message);
}
