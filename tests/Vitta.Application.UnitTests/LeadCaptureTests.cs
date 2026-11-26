using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.LeadCapture;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;

namespace Vitta.Application.UnitTests;

public class LeadCaptureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 15, 0, 0, TimeSpan.Zero); // sabado (Brasilia 12h)

    private static (SubmitLeadHandler Handler, FakeProspectRepository Prospects, FakeContactLogRepository Contacts) Setup(bool enabled = true)
    {
        var settings = LeadCaptureSettings.CreateDefault();
        settings.Update(enabled, "Corretora Exemplo", null, "21999990000");
        var prospects = new FakeProspectRepository();
        var contacts = new FakeContactLogRepository();
        return (new SubmitLeadHandler(new FakeLeadSettings(settings), prospects, contacts, new FakeUnitOfWork(), new FixedTime(Now)), prospects, contacts);
    }

    private static SubmitLeadCommand Lead(string phone = "(21) 98888-7777", string? website = null, string? origin = "instagram") =>
        new("Ana Lima", phone, "ana@x.com", "Rio de Janeiro", "rj", new DateOnly(1985, 3, 2), "proteger a família", origin, true, website);

    [Fact]
    public async Task CreatesProspect_WithConsentOriginAndFollowUp()
    {
        var (handler, prospects, contacts) = Setup();

        Assert.True((await handler.Handle(Lead(), default)).IsSuccess);

        var p = Assert.Single(prospects.Prospects);
        Assert.Equal(("Página de captação · instagram", "RJ", ProspectStage.New), (p.Source, p.State, p.Stage));
        Assert.Equal(Now, p.ConsentAt);
        Assert.Contains("Autorizo Corretora Exemplo", p.ConsentText);
        var log = Assert.Single(contacts.Logs);
        Assert.Equal((p.Id, Vitta.Domain.Contacts.ContactChannel.Form), (log.ProspectId!.Value, log.Channel));
        Assert.Contains("interesse: proteger a família", log.Summary);
        Assert.Null(p.Notes);
        Assert.Equal(new DateOnly(2026, 11, 30), p.NextFollowUpOn); // sabado -> segunda
    }

    [Fact]
    public async Task SamePersonAgain_AddsNoteInsteadOfDuplicating()
    {
        var (handler, prospects, contacts) = Setup();
        await handler.Handle(Lead(), default);

        await handler.Handle(Lead(phone: "21 98888-7777", origin: "whatsapp"), default);

        var p = Assert.Single(prospects.Prospects);
        Assert.Equal(2, contacts.Logs.Count(l => l.ProspectId == p.Id));
        Assert.Contains(contacts.Logs, l => l.Summary.Contains("[origem: whatsapp]"));
    }

    [Fact]
    public async Task Honeypot_IsIgnoredSilently_AndDisabledPageRejects()
    {
        var (handler, prospects, _) = Setup();
        Assert.True((await handler.Handle(Lead(website: "http://spam"), default)).IsSuccess);
        Assert.Empty(prospects.Prospects);

        var (disabled, _, _) = Setup(enabled: false);
        Assert.Equal("LeadCapture.Disabled", (await disabled.Handle(Lead(), default)).Error.Code);
    }

    [Fact]
    public void Settings_RequireBrokerNameToEnable_AndValidateWhatsApp()
    {
        var s = LeadCaptureSettings.CreateDefault();
        Assert.True(s.Update(true, "", null, null).IsFailure);
        Assert.True(s.Update(true, "Corretora", null, "123").IsFailure);
        Assert.True(s.Update(true, "Corretora", null, "(21) 99999-0000").IsSuccess);
        Assert.Equal("21999990000", s.WhatsApp);
        Assert.Equal(LeadCaptureSettings.DefaultHeadline, s.Headline);
    }

    private sealed class FakeLeadSettings(LeadCaptureSettings? saved) : ILeadCaptureSettingsRepository
    {
        public Task<LeadCaptureSettings?> GetAsync(CancellationToken ct = default) => Task.FromResult(saved);
        public void Add(LeadCaptureSettings settings) => saved = settings;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
