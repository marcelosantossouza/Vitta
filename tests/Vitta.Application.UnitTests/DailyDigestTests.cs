using Microsoft.Extensions.Logging.Abstractions;
using Vitta.Application.Abstractions.Notifications;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Notifications;
using Vitta.Application.Features.Policies;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;

namespace Vitta.Application.UnitTests;

public class DailyDigestTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private static DashboardDto Dashboard() => new(
        1, 1, 1, 100m, 2,
        [
            new ProspectDto(Guid.NewGuid(), "Ana <Lima>", "(11) 9999-0000", null, null, ProspectStage.Contacted, Today.AddDays(-2), null, null, default),
            new ProspectDto(Guid.NewGuid(), "Bruno", null, "b@x.com", null, ProspectStage.New, Today, null, null, default),
        ],
        [], [],
        [new PortfolioChangeDto(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1), "MAG", PortfolioChangeType.Left, "123", "Carlos", "52998224725", null, null)],
        null);

    private static readonly BookSummaryDto Book = new(10, 8, 12, 1, 3000m, 330m, 11m, 2);

    [Fact]
    public void Build_HasNumbersFollowUpsAndExits_EscapesHtml_AndHidesCpf()
    {
        var mail = DailyDigest.Build(["a@x.com"], Dashboard(), Book, Today, "https://vitta.exemplo.com/");

        Assert.Contains("2 contatos para hoje", mail.Subject);
        Assert.Contains("Ana &lt;Lima&gt;", mail.Html);           // nome escapado
        Assert.Contains("atrasado desde 23/11", mail.Html);
        Assert.Contains("R$ 330,00", mail.Html);
        Assert.Contains("sem contar 2 apólices sem %", mail.Html);
        Assert.Contains("Carlos", mail.Html);
        Assert.DoesNotContain("52998224725", mail.Html + mail.Text); // CPF nunca vai no e-mail
        Assert.Contains("https://vitta.exemplo.com/painel", mail.Html);
    }

    [Fact]
    public async Task Scheduled_SendsOncePerDay_AndManualDoesNotMarkSent()
    {
        var settings = NotificationSettings.CreateDefault();
        settings.Update(true, ["a@x.com"], new TimeOnly(7, 0));
        var repo = new FakeNotificationSettingsRepository(settings);
        var email = new FakeEmailSender();
        var handler = Handler(repo, email, new DateTimeOffset(2026, 11, 25, 10, 30, 0, TimeSpan.Zero)); // 07:30 em Brasilia

        Assert.Equal(1, (await handler.Handle(new SendDailyDigestCommand(Scheduled: false), default)).Value);
        Assert.Null(settings.LastSentOn);
        Assert.Equal(1, (await handler.Handle(new SendDailyDigestCommand(Scheduled: true), default)).Value);
        Assert.Equal(Today, settings.LastSentOn);
        Assert.Equal(0, (await handler.Handle(new SendDailyDigestCommand(Scheduled: true), default)).Value);
        Assert.Equal(2, email.Sent.Count);
    }

    [Fact]
    public async Task Manual_WithoutSmtp_ReturnsFriendlyError()
    {
        var settings = NotificationSettings.CreateDefault();
        settings.Update(true, ["a@x.com"], new TimeOnly(7, 0));
        var handler = Handler(new FakeNotificationSettingsRepository(settings), new FakeEmailSender { IsConfigured = false }, DateTimeOffset.UtcNow);

        var result = await handler.Handle(new SendDailyDigestCommand(Scheduled: false), default);

        Assert.Equal(NotificationSettingsErrors.EmailNotConfigured, result.Error);
    }

    private static SendDailyDigestHandler Handler(INotificationSettingsRepository repo, IEmailSender email, DateTimeOffset utcNow) =>
        new(repo, new FakeDashboardQueries(Dashboard()), new FakePolicyQueries(Book), email, new FakeUnitOfWork(), new FixedTime(utcNow),
            NullLogger<SendDailyDigestHandler>.Instance);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed class FakeNotificationSettingsRepository(NotificationSettings? saved = null) : INotificationSettingsRepository
{
    public NotificationSettings? Saved { get; private set; } = saved;
    public Task<NotificationSettings?> GetAsync(CancellationToken ct = default) => Task.FromResult(Saved);
    public void Add(NotificationSettings settings) => Saved = settings;
}

internal sealed class FakeEmailSender : IEmailSender
{
    public bool IsConfigured { get; init; } = true;
    public string? From => "vitta@exemplo.com";
    public string? AppUrl => null;
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDashboardQueries(DashboardDto dto) : IDashboardQueries
{
    public Task<DashboardDto> GetAsync(DateOnly today, CancellationToken ct = default) => Task.FromResult(dto);
}

internal sealed class FakePolicyQueries(BookSummaryDto summary) : IPolicyQueries
{
    public Task<PagedResult<PolicyDto>> SearchAsync(PolicyFilter filter, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<PolicyDto>([], page, pageSize, 0));
    public Task<BookSummaryDto> SummaryAsync(CancellationToken ct = default) => Task.FromResult(summary);
}
