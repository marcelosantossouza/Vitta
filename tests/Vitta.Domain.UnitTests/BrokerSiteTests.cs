using Vitta.Domain.Settings;

namespace Vitta.Domain.UnitTests;

public class BrokerSiteTests
{
    private static BrokerSiteData Data(string? host = null, string? instagram = null, string? color = null, IReadOnlyList<string>? products = null) =>
        new(true, host, "Sobre", null, null, null, null, instagram, null, null, "123456", null, products, color);

    [Fact]
    public void Update_NormalizesHostSocialAndProducts()
    {
        var site = BrokerSite.CreateDefault();

        Assert.True(site.Update(Data("https://Argleidson.usevitta.duckdns.org/", "@corretora", null, ["Vida", " ", "Vida em grupo "])).IsSuccess);

        Assert.Equal("argleidson.usevitta.duckdns.org", site.Host);
        Assert.Equal("https://instagram.com/corretora", site.Instagram);
        Assert.Equal(["Vida", "Vida em grupo"], site.Products);
        Assert.Equal(BrokerSite.DefaultAccent, site.AccentColor);
    }

    [Theory]
    [InlineData("nao e dominio", null)]
    [InlineData(null, "verde")]
    public void Update_RejectsInvalidHostOrColor(string? host, string? color) =>
        Assert.True(BrokerSite.CreateDefault().Update(Data(host, color: color)).IsFailure);

    [Fact]
    public void Image_AcceptsPngByContent_RejectsOthers()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        Assert.Equal("image/png", SiteImage.Create("logo", png).Value.ContentType);
        Assert.True(SiteImage.Create("logo", "<svg onload=alert(1)></svg>"u8.ToArray()).IsFailure);
        Assert.True(SiteImage.Create("logo", new byte[SiteImage.MaxBytes + 1]).IsFailure);
    }
}
