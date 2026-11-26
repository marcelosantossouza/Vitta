using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.Auth;
using Vitta.Application.Features.Users;
using Vitta.Domain.Users;

namespace Vitta.Application.UnitTests;

public class UsersAndAuthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 15, 0, 0, TimeSpan.Zero);

    private static User NewUser(string email, UserRole role, string password = "senha-forte", bool temporary = false)
    {
        var u = User.Create(email.Split('@')[0], email, role).Value;
        u.SetPasswordHash(FakeHasher.HashOf(password), temporary);
        return u;
    }

    private static LoginHandler Login(FakeUserRepository repo) =>
        new(repo, new FakeHasher(), new FakeTokens(), new FakeUnitOfWork(), new FixedClock(Now));

    // ---------------- Login ----------------

    [Fact]
    public async Task Login_ReturnsRoleAndMustChangePassword()
    {
        var repo = new FakeUserRepository(NewUser("ana@x.com", UserRole.Assistant, temporary: true));

        var result = await Login(repo).Handle(new LoginCommand("ANA@x.com", "senha-forte"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal((UserRole.Assistant, true), (result.Value.User.Role, result.Value.User.MustChangePassword));
    }

    [Fact]
    public async Task Login_InactiveUser_SameMessageAsWrongPassword()
    {
        var user = NewUser("ana@x.com", UserRole.Broker);
        user.Deactivate();

        var result = await Login(new FakeUserRepository(user)).Handle(new LoginCommand("ana@x.com", "senha-forte"), default);

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_LocksAfterFiveWrongPasswords_EvenWithRightPasswordAfterwards()
    {
        var user = NewUser("ana@x.com", UserRole.Broker);
        var handler = Login(new FakeUserRepository(user));

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(AuthErrors.InvalidCredentials, (await handler.Handle(new LoginCommand("ana@x.com", "errada"), default)).Error);
        }

        Assert.Equal("Auth.LockedOut", (await handler.Handle(new LoginCommand("ana@x.com", "errada"), default)).Error.Code);
        Assert.Equal("Auth.LockedOut", (await handler.Handle(new LoginCommand("ana@x.com", "senha-forte"), default)).Error.Code);
        Assert.True(user.IsLockedOut(Now) && !user.IsLockedOut(Now + User.LockoutDuration));
    }

    // ---------------- Trocar a propria senha ----------------

    [Fact]
    public async Task ChangePassword_ChecksCurrent_AndClearsTemporaryFlag()
    {
        var user = NewUser("ana@x.com", UserRole.Assistant, temporary: true);
        var handler = new ChangePasswordHandler(new FakeCurrentUser(user.Id), new FakeUserRepository(user), new FakeHasher(), new FakeUnitOfWork());

        Assert.Equal(UserErrors.WrongCurrentPassword, (await handler.Handle(new ChangePasswordCommand("x", "nova-senha-1"), default)).Error);
        Assert.Equal(UserErrors.WeakPassword, (await handler.Handle(new ChangePasswordCommand("senha-forte", "curta"), default)).Error);
        Assert.True((await handler.Handle(new ChangePasswordCommand("senha-forte", "nova-senha-1"), default)).IsSuccess);
        Assert.False(user.MustChangePassword);
    }

    // ---------------- Gestao de usuarios ----------------

    [Fact]
    public async Task Create_SetsTemporaryPassword_AndRejectsDuplicateEmail()
    {
        var repo = new FakeUserRepository(NewUser("admin@x.com", UserRole.Admin));
        var handler = new CreateUserHandler(repo, new FakeHasher(), new FakeUnitOfWork());

        var created = await handler.Handle(new CreateUserCommand("Bia", "Bia@X.com", UserRole.Assistant, "provisoria1"), default);
        var duplicate = await handler.Handle(new CreateUserCommand("Outra", "bia@x.com", UserRole.Broker, "provisoria1"), default);

        Assert.True(created.IsSuccess);
        var bia = repo.Users.Single(u => u.Email == "bia@x.com");
        Assert.True(bia.MustChangePassword && bia.IsActive && bia.Role == UserRole.Assistant);
        Assert.Equal(UserErrors.EmailAlreadyExists, duplicate.Error);
    }

    [Fact]
    public async Task CannotChangeOwnRole_OrDeactivateSelf()
    {
        var admin = NewUser("admin@x.com", UserRole.Admin);
        var other = NewUser("outro@x.com", UserRole.Admin);
        var repo = new FakeUserRepository(admin, other);
        var me = new FakeCurrentUser(admin.Id);

        Assert.Equal(UserErrors.CannotChangeOwnAccess,
            (await new UpdateUserHandler(repo, me, new FakeUnitOfWork()).Handle(new UpdateUserCommand(admin.Id, "Eu", UserRole.Broker), default)).Error);
        Assert.Equal(UserErrors.CannotChangeOwnAccess,
            (await new SetUserActiveHandler(repo, me, new FakeUnitOfWork()).Handle(new SetUserActiveCommand(admin.Id, false), default)).Error);
        Assert.True((await new UpdateUserHandler(repo, me, new FakeUnitOfWork()).Handle(new UpdateUserCommand(admin.Id, "Novo nome", UserRole.Admin), default)).IsSuccess);
    }

    [Fact]
    public async Task KeepsAtLeastOneActiveAdmin()
    {
        var admin = NewUser("admin@x.com", UserRole.Admin);
        var broker = NewUser("corretor@x.com", UserRole.Broker);
        var repo = new FakeUserRepository(admin, broker);
        var asBroker = new FakeCurrentUser(broker.Id); // cenario hipotetico: outra pessoa tentando rebaixar o unico admin

        Assert.Equal(UserErrors.LastAdmin,
            (await new UpdateUserHandler(repo, asBroker, new FakeUnitOfWork()).Handle(new UpdateUserCommand(admin.Id, "Admin", UserRole.Broker), default)).Error);
        Assert.Equal(UserErrors.LastAdmin,
            (await new SetUserActiveHandler(repo, asBroker, new FakeUnitOfWork()).Handle(new SetUserActiveCommand(admin.Id, false), default)).Error);
    }

    [Fact]
    public async Task ResetPassword_UnlocksAndForcesChange()
    {
        var user = NewUser("ana@x.com", UserRole.Broker);
        for (var i = 0; i < User.MaxFailedLogins; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        await new ResetUserPasswordHandler(new FakeUserRepository(user), new FakeHasher(), new FakeUnitOfWork())
            .Handle(new ResetUserPasswordCommand(user.Id, "provisoria2"), default);

        Assert.False(user.IsLockedOut(Now));
        Assert.True(user.MustChangePassword);
    }

    // ---------------- Fakes ----------------

    private sealed class FakeHasher : IPasswordHasher
    {
        public static string HashOf(string password) => "h:" + password;
        public string Hash(User user, string password) => HashOf(password);
        public bool Verify(User user, string hash, string password) => hash == HashOf(password);
    }

    private sealed class FakeTokens : ITokenService
    {
        public AccessToken Create(User user) => new("token", Now.AddHours(12));
    }

    private sealed class FakeCurrentUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUserRepository(params User[] users) : IUserRepository
    {
        public List<User> Users { get; } = [.. users];
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(Users.FirstOrDefault(u => u.Email == email));
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(Users.Count > 0);
        public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Users);
        public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
            Task.FromResult(Users.Count(u => u.IsActive && u.Role == UserRole.Admin));
        public void Add(User user) => Users.Add(user);
    }
}
