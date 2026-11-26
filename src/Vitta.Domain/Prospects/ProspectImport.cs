using Vitta.Domain.Common;

namespace Vitta.Domain.Prospects;

/// <summary>Uma importacao de planilha de prospeccao (o "lote"): permite ver o historico e desfazer.</summary>
public sealed class ProspectImport : AggregateRoot
{
    private ProspectImport() { } // EF

    public string FileName { get; private set; } = null!;
    public string TemplateName { get; private set; } = null!;
    public int TotalRows { get; private set; }
    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int Skipped { get; private set; }

    public static ProspectImport Start(string fileName, string templateName) =>
        new() { FileName = Clip(Path.GetFileName(fileName), 200), TemplateName = Clip(templateName, 120) };

    public void Finish(int totalRows, int created, int updated, int skipped)
    {
        TotalRows = totalRows;
        Created = created;
        Updated = updated;
        Skipped = skipped;
        Touch();
    }

    private static string Clip(string value, int max) => value.Length > max ? value[..max] : value;
}
