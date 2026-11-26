using Vitta.Domain.Common;

namespace Vitta.Domain.Prospects;

public enum ProspectStage
{
    New = 1,
    Contacted = 2,
    MeetingScheduled = 3,
    ProposalSent = 4,
    Won = 5,
    Lost = 6,
}

/// <summary>
/// Dados de perfil vindos das listas de prospeccao (endereco, renda...). Todos opcionais e filtraveis.
/// Valores em reais; UF com 2 letras.
/// </summary>
/// <param name="Cnpj">Preenchido = lead e uma empresa (ex.: vida em grupo); so digitos.</param>
/// <param name="Activity">Atividade principal (CNAE), ex.: "8630-5/04 Atividade odontologica".</param>
/// <param name="CompanySize">Porte: ME, EPP ou Demais.</param>
public sealed record ProspectProfile(
    string? Phone2 = null, string? Address = null, string? City = null, string? State = null, string? ZipCode = null,
    DateOnly? BirthDate = null, string? Occupation = null, decimal? EstimatedSalary = null, decimal? Discounts = null,
    decimal? Loans = null, string? Cnpj = null, string? LegalName = null, string? Activity = null, string? CompanySize = null,
    DateOnly? OpenedOn = null, string? Cpf = null)
{
    public static readonly ProspectProfile Empty = new();
}

/// <summary>Possivel cliente no funil de prospeccao.</summary>
public sealed class Prospect : AggregateRoot
{
    private Prospect() { } // EF

    public string Name { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    /// <summary>Origem do lead (indicacao, Instagram, evento...).</summary>
    public string? Source { get; private set; }
    public ProspectStage Stage { get; private set; }
    public DateOnly? NextFollowUpOn { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>Preenchido quando o prospect vira cliente.</summary>
    public Guid? ConvertedClientId { get; private set; }

    // ---- Perfil (listas de prospeccao) ----
    public string? Phone2 { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    /// <summary>UF em maiusculas (2 letras).</summary>
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    /// <summary>Profissao, cargo ou orgao/empregador.</summary>
    public string? Occupation { get; private set; }
    public decimal? EstimatedSalary { get; private set; }
    public decimal? Discounts { get; private set; }
    public decimal? Loans { get; private set; }

    // ---- Empresa (leads B2B, ex.: vida em grupo) ----
    /// <summary>So digitos (14). Preenchido = o lead e uma empresa.</summary>
    public string? Cnpj { get; private set; }
    public string? LegalName { get; private set; }
    public string? Activity { get; private set; }
    public string? CompanySize { get; private set; }
    public DateOnly? OpenedOn { get; private set; }
    public bool IsCompany => Cnpj is not null;

    /// <summary>Quando a pessoa aceitou ser contatada (pagina de captacao). Nulo = lead de lista/importacao/cadastro manual.</summary>
    public DateTimeOffset? ConsentAt { get; private set; }

    /// <summary>Texto exato que a pessoa aceitou (prova do consentimento, LGPD art. 8).</summary>
    public string? ConsentText { get; private set; }

    public void RecordConsent(DateTimeOffset at, string text)
    {
        ConsentAt = at;
        ConsentText = text.Length > 600 ? text[..600] : text;
        Touch();
    }

    /// <summary>Acrescenta uma linha nas anotacoes (ex.: novo pedido de contato pela pagina).</summary>
    public void AppendNote(string line)
    {
        var notes = string.IsNullOrWhiteSpace(Notes) ? line : $"{Notes}\n{line}";
        Notes = notes.Length > 2000 ? notes[^2000..] : notes;
        Touch();
    }

    /// <summary>CPF da pessoa (so digitos, validado). Opcional: muitos leads chegam so com nome e telefone.</summary>
    public string? Cpf { get; private set; }

    /// <summary>Importacao de planilha que criou o prospect (nulo = cadastrado a mao ou importacao ja excluida).</summary>
    public Guid? ImportId { get; private set; }

    public ProspectProfile Profile =>
        new(Phone2, Address, City, State, ZipCode, BirthDate, Occupation, EstimatedSalary, Discounts, Loans,
            Cnpj, LegalName, Activity, CompanySize, OpenedOn, Cpf);

    /// <summary>Ainda nao foi trabalhado: continua em "Novo" e nao virou cliente (pode sair junto com a importacao).</summary>
    public bool IsUntouchedLead => Stage == ProspectStage.New && ConvertedClientId is null;

    public static Result<Prospect> Create(string name, string? phone, string? email, string? source, string? notes = null, DateOnly? nextFollowUpOn = null)
    {
        var prospect = new Prospect();
        var result = prospect.Update(name, phone, email, source, notes, nextFollowUpOn);
        if (result.IsFailure)
        {
            return result.Error;
        }

        prospect.Stage = ProspectStage.New;
        return prospect;
    }

    public Result Update(string name, string? phone, string? email, string? source, string? notes, DateOnly? nextFollowUpOn)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProspectErrors.NameRequired;
        }

        Name = name.Trim();
        Phone = Clean(phone);
        Email = Clean(email);
        Source = Clean(source);
        Notes = Clean(notes);
        NextFollowUpOn = nextFollowUpOn;
        Touch();
        return Result.Success();
    }

    public bool IsOpen => Stage is not (ProspectStage.Won or ProspectStage.Lost);

    public Result SetProfile(ProspectProfile profile)
    {
        if (profile.EstimatedSalary is < 0 || profile.Discounts is < 0 || profile.Loans is < 0)
        {
            return ProspectErrors.NegativeAmount;
        }

        string? cpf = null;
        if (!string.IsNullOrWhiteSpace(profile.Cpf))
        {
            var valid = Common.Cpf.Create(profile.Cpf);
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            cpf = valid.Value.Value;
        }

        Phone2 = Clip(profile.Phone2, 30);
        Address = Clip(profile.Address, 300);
        City = Clip(profile.City, 100);
        State = NormalizeState(profile.State);
        ZipCode = Clip(profile.ZipCode, 20);
        BirthDate = profile.BirthDate;
        Occupation = Clip(profile.Occupation, 150);
        EstimatedSalary = profile.EstimatedSalary;
        Discounts = profile.Discounts;
        Loans = profile.Loans;
        Cnpj = NormalizeCnpj(profile.Cnpj);
        LegalName = Clip(profile.LegalName, 200);
        Activity = Clip(profile.Activity, 250);
        CompanySize = Clip(profile.CompanySize, 30);
        OpenedOn = profile.OpenedOn;
        Cpf = cpf;
        Touch();
        return Result.Success();
    }

    /// <summary>
    /// Completa so o que esta vazio com dados de uma planilha nova (nunca apaga nem troca o que o corretor ja tem).
    /// Devolve true quando algo mudou.
    /// </summary>
    public bool FillMissing(string? phone, string? email, string? source, ProspectProfile p)
    {
        var before = (Phone, Email, Source, Profile);
        Phone ??= Clean(phone);
        Email ??= Clean(email);
        Source ??= Clean(source);
        Phone2 ??= Clip(p.Phone2, 30);
        Address ??= Clip(p.Address, 300);
        City ??= Clip(p.City, 100);
        State ??= NormalizeState(p.State);
        ZipCode ??= Clip(p.ZipCode, 20);
        BirthDate ??= p.BirthDate;
        Occupation ??= Clip(p.Occupation, 150);
        EstimatedSalary ??= p.EstimatedSalary is >= 0 ? p.EstimatedSalary : null;
        Discounts ??= p.Discounts is >= 0 ? p.Discounts : null;
        Loans ??= p.Loans is >= 0 ? p.Loans : null;
        Cnpj ??= NormalizeCnpj(p.Cnpj);
        LegalName ??= Clip(p.LegalName, 200);
        Activity ??= Clip(p.Activity, 250);
        CompanySize ??= Clip(p.CompanySize, 30);
        OpenedOn ??= p.OpenedOn;
        Cpf ??= Common.Cpf.Create(p.Cpf).IsSuccess ? Common.Cpf.Create(p.Cpf).Value.Value : null;
        var changed = before != (Phone, Email, Source, Profile);
        if (changed)
        {
            Touch();
        }

        return changed;
    }

    public void AssignImport(Guid importId) => ImportId = importId;

    /// <summary>A importacao foi excluida mas o prospect ja tinha sido trabalhado: fica, sem vinculo.</summary>
    public void DetachImport() => ImportId = null;

    private static string? Clip(string? value, int max) =>
        Clean(value) is { } v ? (v.Length > max ? v[..max] : v) : null;

    /// <summary>"12.345.678/0001-90" -> "12345678000190". Sem 14 digitos fica vazio (nao e CNPJ).</summary>
    private static string? NormalizeCnpj(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 14 ? digits : null;
    }

    /// <summary>"sp", " SP " -> "SP". Texto maior que 2 letras (ex.: "Sao Paulo") fica como veio, limitado.</summary>
    private static string? NormalizeState(string? value) =>
        Clean(value) is { } v ? (v.Length == 2 ? v.ToUpperInvariant() : Clip(v, 30)) : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void MoveTo(ProspectStage stage, DateOnly? nextFollowUpOn = null)
    {
        Stage = stage;
        NextFollowUpOn = stage is ProspectStage.Won or ProspectStage.Lost ? null : nextFollowUpOn;
        Touch();
    }

    public Result MarkConverted(Guid clientId)
    {
        if (ConvertedClientId is not null)
        {
            return ProspectErrors.AlreadyConverted;
        }

        ConvertedClientId = clientId;
        MoveTo(ProspectStage.Won);
        return Result.Success();
    }
}

public static class ProspectErrors
{
    public static readonly Error NegativeAmount = Error.Validation("Prospect.NegativeAmount", "Salário, descontos e empréstimos não podem ser negativos.");
    public static readonly Error NameRequired = Error.Validation("Prospect.NameRequired", "Nome é obrigatório.");
    public static readonly Error AlreadyConverted = Error.Conflict("Prospect.AlreadyConverted", "Este prospect ja foi convertido em cliente.");
    public static Error NotFound(Guid id) => Error.NotFound("Prospect.NotFound", "Prospect não encontrado.");
}
