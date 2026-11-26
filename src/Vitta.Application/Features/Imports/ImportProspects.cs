using Mediator;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Prospects;
using F = Vitta.Domain.Imports.ImportFields.Prospects;

namespace Vitta.Application.Features.Imports;

/// <param name="Updated">Prospects que ja existiam e tiveram campos vazios completados (so com "atualizar quem ja existe").</param>
/// <param name="ImportId">Lote criado (aparece no historico e pode ser desfeito).</param>
public sealed record ProspectImportResultDto(
    int TotalRows, int Created, int Skipped, IReadOnlyList<string> Messages, int Updated = 0, Guid? ImportId = null);

/// <summary>
/// Importa uma planilha de possiveis clientes usando um modelo. Novos entram na etapa "Novo" ligados ao lote.
/// Repetido (mesmo e-mail, telefone ou nome sem contato) e ignorado - ou, com <paramref name="UpdateExisting"/>,
/// tem so os campos vazios completados com os dados da planilha.
/// </summary>
public sealed record ImportProspectsCommand(Guid TemplateId, string FileName, Stream File, bool UpdateExisting = false)
    : ICommand<Result<ProspectImportResultDto>>;

public sealed class ImportProspectsHandler(
    IImportTemplateRepository templates,
    IProspectRepository prospects,
    IProspectImportRepository imports,
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
            seen.Add(existing, existing.Name, existing.Phone, existing.Email);
        }

        var batch = ProspectImport.Start(c.FileName, template.Name);
        var messages = new List<string>();
        void Note(string message)
        {
            if (messages.Count < MaxMessages)
            {
                messages.Add(message);
            }
        }

        int created = 0, updated = 0, skipped = 0;
        var updatedIds = new HashSet<Guid>();
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
            var source = Limit(row.Get(F.Source), 100);
            var profile = ProfileOf(row);

            if (seen.Find(name, phone, email) is { } existing)
            {
                if (c.UpdateExisting && existing.FillMissing(phone, email, source, profile) && updatedIds.Add(existing.Id))
                {
                    updated++;
                    Note($"Linha {row.RowNumber}: {name} já estava na prospecção - dados completados.");
                }
                else
                {
                    skipped++;
                    Note($"Linha {row.RowNumber}: {name} já está na prospecção.");
                }

                continue;
            }

            var followUpText = row.Get(F.NextFollowUpOn);
            var followUp = SpreadsheetMapper.ParseDate(followUpText);
            var notes = string.Join('\n', row.All(F.Notes).Select(n => $"{n.Column}: {n.Value}"));
            if (followUpText is not null && followUp is null)
            {
                notes = string.Join('\n', new[] { notes, $"Próximo contato: {followUpText}" }.Where(s => s.Length > 0));
            }

            var prospect = Prospect.Create(name, phone, email, source, Limit(notes, 2000), followUp);
            var withProfile = prospect.IsSuccess ? prospect.Value.SetProfile(profile) : Result.Success();
            if (prospect.IsFailure || withProfile.IsFailure)
            {
                skipped++;
                Note($"Linha {row.RowNumber}: {(prospect.IsFailure ? prospect.Error : withProfile.Error).Message}");
                continue;
            }

            prospect.Value.AssignImport(batch.Id);
            prospects.Add(prospect.Value);
            seen.Add(prospect.Value, name, phone, email);
            created++;
        }

        batch.Finish(rows.Value.Count, created, updated, skipped);
        imports.Add(batch);
        template.MarkUsed(clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return new ProspectImportResultDto(rows.Value.Count, created, skipped, messages, updated, batch.Id);
    }

    /// <summary>Campos de perfil da linha. Valores invalidos (texto em coluna de salario, data impossivel) ficam vazios.</summary>
    private static ProspectProfile ProfileOf(MappedRow row)
    {
        static decimal? Amount(string? text) => SpreadsheetMapper.ParseMoney(text) is >= 0 and var v ? v : null;
        return new ProspectProfile(
            Phone2: Limit(row.Get(F.Phone2), 30),
            Address: Limit(row.Get(F.Address), 300),
            City: Limit(row.Get(F.City), 100),
            State: Limit(row.Get(F.State), 30),
            ZipCode: Limit(row.Get(F.ZipCode), 20),
            BirthDate: SpreadsheetMapper.ParseDate(row.Get(F.BirthDate)),
            Occupation: Limit(row.Get(F.Occupation), 150),
            EstimatedSalary: Amount(row.Get(F.EstimatedSalary)),
            Discounts: Amount(row.Get(F.Discounts)),
            Loans: Amount(row.Get(F.Loans)),
            Cnpj: row.Get(F.Cnpj),
            LegalName: Limit(row.Get(F.LegalName), 200),
            Activity: Limit(row.Get(F.Activity), 250),
            CompanySize: Limit(row.Get(F.CompanySize), 30),
            OpenedOn: SpreadsheetMapper.ParseDate(row.Get(F.OpenedOn)));
    }

    private static string? Limit(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();

    /// <summary>Duplicado = mesmo e-mail, mesmo telefone (ultimos 9 digitos) ou mesmo nome quando nao ha contato.</summary>
    private sealed class DuplicateIndex
    {
        private readonly Dictionary<string, Prospect> _byKey = [];

        public void Add(Prospect prospect, string name, string? phone, string? email)
        {
            foreach (var key in Keys(name, phone, email))
            {
                _byKey.TryAdd(key, prospect);
            }
        }

        public Prospect? Find(string name, string? phone, string? email) =>
            Keys(name, phone, email).Select(k => _byKey.GetValueOrDefault(k)).FirstOrDefault(p => p is not null);

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
