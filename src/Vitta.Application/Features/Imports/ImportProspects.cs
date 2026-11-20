using Mediator;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Prospects;
using F = Vitta.Domain.Imports.ImportFields.Prospects;

namespace Vitta.Application.Features.Imports;

public sealed record ProspectImportResultDto(int TotalRows, int Created, int Skipped, IReadOnlyList<string> Messages);

/// <summary>Importa uma planilha de possiveis clientes usando um modelo. Todos entram na etapa "Novo".</summary>
public sealed record ImportProspectsCommand(Guid TemplateId, string FileName, Stream File) : ICommand<Result<ProspectImportResultDto>>;

public sealed class ImportProspectsHandler(
    IImportTemplateRepository templates,
    IProspectRepository prospects,
    ISpreadsheetReader reader,
    TimeProvider clock,
    IUnitOfWork uow) : ICommandHandler<ImportProspectsCommand, Result<ProspectImportResultDto>>
{
    private const int MaxMessages = 50;

    public async ValueTask<Result<ProspectImportResultDto>> Handle(ImportProspectsCommand c, CancellationToken ct)
    {
        var template = await templates.GetByIdAsync(c.TemplateId, ct);
        if (template is null)
        {
            return ImportTemplateErrors.NotFound(c.TemplateId);
        }

        if (template.Kind != ImportTemplateKind.Prospects)
        {
            return ImportTemplateErrors.WrongKind;
        }

        var data = await reader.ReadAsync(c.File, c.FileName, template.SheetName, ct);
        if (data.IsFailure)
        {
            return data.Error;
        }

        var rows = SpreadsheetMapper.Apply(data.Value, template.HeaderRow, template.Columns, template.Name);
        if (rows.IsFailure)
        {
            return rows.Error;
        }

        var seen = new DuplicateIndex();
        foreach (var existing in await prospects.ListAllAsync(ct))
        {
            seen.Add(existing.Name, existing.Phone, existing.Email);
        }

        var messages = new List<string>();
        void Note(string message)
        {
            if (messages.Count < MaxMessages)
            {
                messages.Add(message);
            }
        }

        int created = 0, skipped = 0;
        foreach (var row in rows.Value)
        {
            var name = Limit(row.Get(F.Name), 200);
            if (name is null)
            {
                skipped++;
                Note($"Linha {row.RowNumber}: sem nome, ignorada.");
                continue;
            }

            var phone = Limit(row.Get(F.Phone), 30);
            var email = Limit(row.Get(F.Email), 200);
            if (seen.Contains(name, phone, email))
            {
                skipped++;
                Note($"Linha {row.RowNumber}: {name} já está na prospecção.");
                continue;
            }

            var followUpText = row.Get(F.NextFollowUpOn);
            var followUp = SpreadsheetMapper.ParseDate(followUpText);
            var notes = string.Join('\n', row.All(F.Notes).Select(n => $"{n.Column}: {n.Value}"));
            if (followUpText is not null && followUp is null)
            {
                notes = string.Join('\n', new[] { notes, $"Próximo contato: {followUpText}" }.Where(s => s.Length > 0));
            }

            var prospect = Prospect.Create(name, phone, email, Limit(row.Get(F.Source), 100), notes, followUp);
            if (prospect.IsFailure)
            {
                skipped++;
                Note($"Linha {row.RowNumber}: {prospect.Error.Message}");
                continue;
            }

            prospects.Add(prospect.Value);
            seen.Add(name, phone, email);
            created++;
        }

        template.MarkUsed(clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return new ProspectImportResultDto(rows.Value.Count, created, skipped, messages);
    }

    private static string? Limit(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();

    /// <summary>Duplicado = mesmo e-mail, mesmo telefone (ultimos 9 digitos) ou mesmo nome quando nao ha contato.</summary>
    private sealed class DuplicateIndex
    {
        private readonly HashSet<string> _keys = [];

        public void Add(string name, string? phone, string? email)
        {
            foreach (var key in Keys(name, phone, email))
            {
                _keys.Add(key);
            }
        }

        public bool Contains(string name, string? phone, string? email) => Keys(name, phone, email).Any(_keys.Contains);

        private static IEnumerable<string> Keys(string name, string? phone, string? email)
        {
            var hasContact = false;
            if (!string.IsNullOrWhiteSpace(email))
            {
                hasContact = true;
                yield return "e:" + email.Trim().ToLowerInvariant();
            }

            var digits = SpreadsheetMapper.Digits(phone);
            if (digits.Length >= 8)
            {
                hasContact = true;
                yield return "p:" + (digits.Length > 9 ? digits[^9..] : digits);
            }

            if (!hasContact)
            {
                yield return "n:" + TextNormalizer.ForSearch(name);
            }
        }
    }
}
