using Vitta.Domain.Common;

namespace Vitta.Domain.Portfolio;

public enum PortfolioImportStatus
{
    Pending = 1,
    Processed = 2,
    Failed = 3,
}

/// <summary>
/// Importacao mensal da planilha de carteira enviada pela seguradora (ex.: relatorio da MAG).
/// Guarda um "retrato" (snapshot) das linhas para ser comparado com a importacao do mes seguinte.
/// </summary>
public sealed class PortfolioImport : AggregateRoot
{
    private readonly List<PortfolioChange> _changes = [];
    private readonly List<PortfolioSnapshotRow> _rows = [];

    private PortfolioImport() { } // EF

    private PortfolioImport(Guid insurerId, DateOnly referenceMonth, string fileName)
    {
        InsurerId = insurerId;
        ReferenceMonth = referenceMonth;
        FileName = fileName;
        Status = PortfolioImportStatus.Pending;
    }

    public Guid InsurerId { get; private set; }
    /// <summary>Primeiro dia do mes de referencia.</summary>
    public DateOnly ReferenceMonth { get; private set; }
    public string FileName { get; private set; } = null!;
    public PortfolioImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    /// <summary>Primeira importacao da seguradora: serve de base, nao gera mudancas.</summary>
    public bool IsFirstImport { get; private set; }
    public IReadOnlyCollection<PortfolioChange> Changes => _changes.AsReadOnly();
    public IReadOnlyCollection<PortfolioSnapshotRow> Rows => _rows.AsReadOnly();

    public static PortfolioImport Create(Guid insurerId, DateOnly referenceMonth, string fileName) =>
        new(insurerId, FirstDayOf(referenceMonth), fileName);

    public static DateOnly FirstDayOf(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Processa as linhas do mes comparando com a importacao anterior (null = primeira importacao).</summary>
    public void Process(IReadOnlyList<PortfolioRow> rows, PortfolioImport? previous)
    {
        _rows.Clear();
        _rows.AddRange(rows.Select(PortfolioSnapshotRow.From));
        TotalRows = rows.Count;

        _changes.Clear();
        IsFirstImport = previous is null;
        if (previous is not null)
        {
            _changes.AddRange(PortfolioReconciler.Reconcile(previous.Rows.Select(r => r.ToRow()), rows));
        }

        Status = PortfolioImportStatus.Processed;
        Touch();
    }
}

public static class PortfolioErrors
{
    public static readonly Error MonthAlreadyImported = Error.Conflict("Portfolio.MonthAlreadyImported", "Já existe importação desta seguradora para este mês. Exclua a anterior para reimportar.");
    public static readonly Error EmptyFile = Error.Validation("Portfolio.EmptyFile", "Nenhuma linha com número de apólice (ou CPF) foi encontrada na planilha.");
    public static readonly Error InsurerRequired = Error.Validation("Portfolio.InsurerRequired", "Escolha a seguradora da planilha.");
    public static Error CompetenceMismatch(DateOnly fileMonth, DateOnly chosen) => Error.Validation("Portfolio.CompetenceMismatch",
        $"O arquivo é da competência {fileMonth:MM/yyyy}, mas o mês escolhido é {chosen:MM/yyyy}. Confira o mês de referência.");
    public static Error InvalidFile(string message) => Error.Validation("Portfolio.InvalidFile", message);
    public static Error NotFound(Guid id) => Error.NotFound("Portfolio.NotFound", "Importação não encontrada.");
}
