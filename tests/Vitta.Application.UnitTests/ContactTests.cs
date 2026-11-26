using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.Contacts;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Contacts;
using Vitta.Domain.Prospects;
using Vitta.Domain.Users;

namespace Vitta.Application.UnitTests;

public class ContactTests
{
    private readonly FakeProspectRepository _prospects = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeContactLogRepository _logs = new();
    private readonly User _user = User.Create("Marina Corretora", "marina@x.com", UserRole.Broker).Value;

    private RegisterContactHandler Handler() =>
        new(_logs, _prospects, _clients, new Users(_user), new CurrentUser(_user.Id), new FakeUnitOfWork(), TimeProvider.System);

    [Fact]
    public async Task Register_SavesWhoAndWhat_AdvancesNewToContacted_AndSchedulesNext()
    {
        var p = Prospect.Create("Ana", "21999990000", null, null).Value;
        _prospects.Add(p);
        var next = new DateOnly(2026, 12, 3);

        var r = await Handler().Handle(new RegisterContactCommand(p.Id, null, ContactChannel.WhatsApp, " Mandei a proposta ", null, next, AdvanceStage: true), default);

        var log = Assert.Single(_logs.Logs);
        Assert.Equal((r.Value, "Mandei a proposta", "Marina Corretora", ContactChannel.WhatsApp), (log.Id, log.Summary, log.UserName, log.Channel));
        Assert.Equal((ProspectStage.Contacted, next), (p.Stage, p.NextFollowUpOn!.Value));
    }

    [Fact]
    public async Task Register_WithoutAdvance_KeepsStage_AndEmptySummaryFails()
    {
        var p = Prospect.Create("Ana", null, null, null).Value;
        _prospects.Add(p);

        await Handler().Handle(new RegisterContactCommand(p.Id, null, ContactChannel.Call, "Não atendeu", null, null, AdvanceStage: false), default);
        Assert.Equal(ProspectStage.New, p.Stage);

        Assert.True(ContactLog.Create(p.Id, null, DateTimeOffset.UtcNow, ContactChannel.Call, "  ", null, null).IsFailure);
        Assert.True(ContactLog.Create(p.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, ContactChannel.Call, "x", null, null).IsFailure); // so um alvo
    }

    [Fact]
    public async Task Conversion_MovesHistoryToClient_AndLaterContactsGoToClient()
    {
        var p = Prospect.Create("Carla", null, null, null).Value;
        _prospects.Add(p);
        await Handler().Handle(new RegisterContactCommand(p.Id, null, ContactChannel.Visit, "Visita", null, null, false), default);

        var clientId = (await new ConvertProspectHandler(_prospects, _clients, _logs, new FakeUnitOfWork())
            .Handle(new ConvertProspectCommand(p.Id, "529.982.247-25", null), default)).Value;
        await Handler().Handle(new RegisterContactCommand(p.Id, null, ContactChannel.Call, "Pós-venda", null, null, false), default);

        Assert.All(_logs.Logs, l => Assert.Equal((clientId, (Guid?)null), (l.ClientId!.Value, l.ProspectId)));
        Assert.Equal(2, _logs.Logs.Count);
    }

    private sealed class CurrentUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
    }

    private sealed class Users(params User[] users) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(users.FirstOrDefault(u => u.Email == email));
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(users.Length > 0);
        public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(users);
        public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void Add(User user) { }
    }
}
