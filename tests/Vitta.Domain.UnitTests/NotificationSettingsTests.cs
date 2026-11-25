using Vitta.Domain.Settings;

namespace Vitta.Domain.UnitTests;

public class NotificationSettingsTests
{
    [Fact]
    public void Update_NormalizesAndDeduplicatesEmails()
    {
        var s = NotificationSettings.CreateDefault();

        var result = s.Update(true, [" Ana@Exemplo.com ", "ana@exemplo.com", "", "bruno@corretora.com.br"], new TimeOnly(7, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(["ana@exemplo.com", "bruno@corretora.com.br"], s.Recipients);
        Assert.Equal(new TimeOnly(7, 30), s.SendAt);
    }

    [Theory]
    [InlineData("nao-e-email")]
    [InlineData("ana@exemplo")]
    [InlineData("Ana <ana@exemplo.com>")]
    public void Update_RejectsInvalidEmail(string email) =>
        Assert.True(NotificationSettings.CreateDefault().Update(false, [email], new TimeOnly(7, 0)).IsFailure);

    [Fact]
    public void Enabling_RequiresRecipients() =>
        Assert.Equal(NotificationSettingsErrors.RecipientsRequired, NotificationSettings.CreateDefault().Update(true, [], new TimeOnly(7, 0)).Error);

    [Fact]
    public void IsDue_OncePerDay_AfterTheTime()
    {
        var s = NotificationSettings.CreateDefault();
        s.Update(true, ["ana@exemplo.com"], new TimeOnly(7, 0));

        Assert.False(s.IsDue(new DateTime(2026, 11, 25, 6, 59, 0)));
        Assert.True(s.IsDue(new DateTime(2026, 11, 25, 7, 0, 0)));
        s.MarkSent(new DateOnly(2026, 11, 25));
        Assert.False(s.IsDue(new DateTime(2026, 11, 25, 15, 0, 0)));
        Assert.True(s.IsDue(new DateTime(2026, 11, 26, 7, 1, 0)));
    }

    [Fact]
    public void IsDue_FalseWhenDisabled()
    {
        var s = NotificationSettings.CreateDefault();
        s.Update(false, ["ana@exemplo.com"], new TimeOnly(7, 0));
        Assert.False(s.IsDue(new DateTime(2026, 11, 25, 9, 0, 0)));
    }
}
