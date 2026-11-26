using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Common;
using Vitta.Domain.Common;
using Vitta.Domain.Contacts;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;

namespace Vitta.Application.Features.LeadCapture;

// Pagina publica de captacao: a pessoa pede contato e ACEITA ser procurada (consentimento, LGPD art. 7, I e art. 8).

/// <summary>O que a pagina publica precisa para se montar (sem dados internos).</summary>
public sealed record LeadPageDto(bool Enabled, string BrokerName, string Headline, string? WhatsApp, string ConsentText);

/// <summary>Configuracao (tela Configuracoes, so administradores).</summary>
public sealed record LeadCaptureSettingsDto(bool Enabled, string BrokerName, string Headline, string? WhatsApp);

// ---------- Publico: dados da pagina ----------

public sealed record GetLeadPageQuery : IQuery<LeadPageDto>;

public sealed class GetLeadPageHandler(ILeadCaptureSettingsRepository settings) : IQueryHandler<GetLeadPageQuery, LeadPageDto>
{
    public async ValueTask<LeadPageDto> Handle(GetLeadPageQuery q, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct) ?? LeadCaptureSettings.CreateDefault();
        return new LeadPageDto(s.Enabled, s.BrokerName, s.Headline, s.WhatsApp, s.ConsentText);
    }
}

// ---------- Publico: enviar pedido de contato ----------

/// <param name="Origin">De onde veio (?origem= no link: instagram, whatsapp, convenio-x...).</param>
/// <param name="Website">Campo invisivel ("pote de mel"): pessoas nao preenchem, robos sim.</param>
public sealed record SubmitLeadCommand(
    string Name, string Phone, string? Email, string? City, string? State, DateOnly? BirthDate, string? Interest,
    string? Origin, bool Consent, string? Website) : ICommand<Result>;

internal sealed class SubmitLeadValidator : AbstractValidator<SubmitLeadCommand>
{
    public SubmitLeadValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithName("Nome");
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.Count(char.IsDigit) is >= 10 and <= 13).WithMessage("Informe o telefone com DDD.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithName("E-mail");
        RuleFor(x => x.Consent).Equal(true).WithMessage("É preciso autorizar o contato para enviar.");
        RuleFor(x => x.Interest).MaximumLength(200);
        RuleFor(x => x.Origin).MaximumLength(60);
    }
}

public sealed class SubmitLeadHandler(
    ILeadCaptureSettingsRepository settings, IProspectRepository prospects, IContactLogRepository contacts, IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<SubmitLeadCommand, Result>
{
    public async ValueTask<Result> Handle(SubmitLeadCommand c, CancellationToken ct)
    {
        var page = await settings.GetAsync(ct);
        if (page is not { Enabled: true })
        {
            return Error.NotFound("LeadCapture.Disabled", "Página indisponível.");
        }

        // Robo preencheu o campo invisivel: finge sucesso e descarta.
        if (!string.IsNullOrWhiteSpace(c.Website))
        {
            return Result.Success();
        }

        var now = time.GetUtcNow();
        var source = Clip("Página de captação" + (string.IsNullOrWhiteSpace(c.Origin) ? string.Empty : " · " + c.Origin.Trim()), 100);
        var profile = new ProspectProfile(City: c.City, State: c.State, BirthDate: c.BirthDate);
        var request = "Pediu contato pela página"
                      + (string.IsNullOrWhiteSpace(c.Interest) ? string.Empty : $" (interesse: {c.Interest.Trim()})")
                      + (string.IsNullOrWhiteSpace(c.Origin) ? string.Empty : $" [origem: {c.Origin.Trim()}]");

        // Mesma pessoa pedindo de novo (telefone ou e-mail): completa os dados e anota, sem duplicar.
        var phoneDigits = Digits(c.Phone);
        var existing = (await prospects.ListAllAsync(ct)).FirstOrDefault(p =>
            (phoneDigits.Length >= 10 && (Last9(p.Phone) == Last9(phoneDigits) || Last9(p.Phone2) == Last9(phoneDigits)))
            || (!string.IsNullOrWhiteSpace(c.Email) && string.Equals(p.Email, c.Email.Trim(), StringComparison.OrdinalIgnoreCase)));
        if (existing is not null)
        {
            existing.FillMissing(c.Phone, c.Email, source, profile);
            existing.RecordConsent(now, page.ConsentText);
            AddRequestToHistory(existing.Id, request, now);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }

        var created = Prospect.Create(c.Name, c.Phone, c.Email, source, null, NextBusinessDay(time.LocalNow()));
        if (created.IsFailure)
        {
            return created.Error;
        }

        created.Value.SetProfile(profile);
        created.Value.RecordConsent(now, page.ConsentText);
        prospects.Add(created.Value);
        AddRequestToHistory(created.Value.Id, request, now);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>O pedido entra no historico de contatos do prospect (canal: formulario).</summary>
    private void AddRequestToHistory(Guid prospectId, string request, DateTimeOffset now)
    {
        if (ContactLog.Create(prospectId, null, now, ContactChannel.Form, request, null, "Página de captação") is { IsSuccess: true } log)
        {
            contacts.Add(log.Value);
        }
    }

    /// <summary>Quem pediu contato deve ser procurado logo: agenda para hoje (ou o proximo dia util se for fim de semana).</summary>
    private static DateOnly NextBusinessDay(DateTime local)
    {
        var day = DateOnly.FromDateTime(local);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    private static string Digits(string? v) => new((v ?? string.Empty).Where(char.IsDigit).ToArray());
    private static string Last9(string? v) => Digits(v) is var d && d.Length > 9 ? d[^9..] : Digits(v);
    private static string Clip(string v, int max) => v.Length > max ? v[..max] : v;
}

// ---------- Configuracao (administradores) ----------

public sealed record GetLeadCaptureSettingsQuery : IQuery<LeadCaptureSettingsDto>;

public sealed class GetLeadCaptureSettingsHandler(ILeadCaptureSettingsRepository settings) : IQueryHandler<GetLeadCaptureSettingsQuery, LeadCaptureSettingsDto>
{
    public async ValueTask<LeadCaptureSettingsDto> Handle(GetLeadCaptureSettingsQuery q, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct) ?? LeadCaptureSettings.CreateDefault();
        return new LeadCaptureSettingsDto(s.Enabled, s.BrokerName, s.Headline, s.WhatsApp);
    }
}

public sealed record UpdateLeadCaptureSettingsCommand(bool Enabled, string? BrokerName, string? Headline, string? WhatsApp) : ICommand<Result>;

public sealed class UpdateLeadCaptureSettingsHandler(ILeadCaptureSettingsRepository settings, IUnitOfWork uow)
    : ICommandHandler<UpdateLeadCaptureSettingsCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateLeadCaptureSettingsCommand c, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        var target = current ?? LeadCaptureSettings.CreateDefault();
        var result = target.Update(c.Enabled, c.BrokerName, c.Headline, c.WhatsApp);
        if (result.IsFailure)
        {
            return result;
        }

        if (current is null)
        {
            settings.Add(target);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
