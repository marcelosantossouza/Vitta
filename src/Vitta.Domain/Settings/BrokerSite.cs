using System.Text.RegularExpressions;
using Vitta.Domain.Common;

namespace Vitta.Domain.Settings;

/// <summary>Dados da home page publica da corretora. Nome, titulo e WhatsApp vem da pagina de captacao.</summary>
public sealed record BrokerSiteData(
    bool Enabled, string? Host, string? About, string? Address, string? Phone, string? Email, string? Hours,
    string? Instagram, string? Facebook, string? LinkedIn, string? Susep, string? Cnpj, IReadOnlyList<string>? Products, string? AccentColor);

/// <summary>Home page da corretora (registro unico enquanto a Vitta nao e multi-corretora).</summary>
public sealed partial class BrokerSite : AggregateRoot
{
    public static readonly Guid SingletonId = Guid.Parse("0199a000-0000-7000-8000-0000000000a4");
    public const string DefaultAccent = "#0f6e5c";

    private BrokerSite() { } // EF

    public bool Enabled { get; private set; }

    /// <summary>Endereco onde a home abre na raiz (ex.: argleidson.usevitta.duckdns.org). Vazio = so em /corretora.</summary>
    public string? Host { get; private set; }

    public string? About { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Hours { get; private set; }
    public string? Instagram { get; private set; }
    public string? Facebook { get; private set; }
    public string? LinkedIn { get; private set; }
    public string? Susep { get; private set; }
    public string? Cnpj { get; private set; }

    /// <summary>Produtos, um por linha.</summary>
    public string? ProductsText { get; private set; }

    public string AccentColor { get; private set; } = DefaultAccent;

    public IReadOnlyList<string> Products =>
        (ProductsText ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static BrokerSite CreateDefault() => new() { Id = SingletonId };

    public Result Update(BrokerSiteData d)
    {
        var host = Clean(d.Host, 120)?.ToLowerInvariant().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        if (host is not null && !HostPattern().IsMatch(host))
        {
            return Error.Validation("Site.InvalidHost", "Endereço inválido. Use só o domínio, ex.: corretora.usevitta.duckdns.org");
        }

        var email = Clean(d.Email, 200);
        if (email is not null && !email.Contains('@'))
        {
            return Error.Validation("Site.InvalidEmail", "E-mail inválido.");
        }

        var color = Clean(d.AccentColor, 7);
        if (color is not null && !ColorPattern().IsMatch(color))
        {
            return Error.Validation("Site.InvalidColor", "Cor inválida (use o formato #RRGGBB).");
        }

        Enabled = d.Enabled;
        Host = host;
        About = Clean(d.About, 2000);
        Address = Clean(d.Address, 250);
        Phone = Clean(d.Phone, 30);
        Email = email;
        Hours = Clean(d.Hours, 120);
        Instagram = Social(d.Instagram, "https://instagram.com/");
        Facebook = Social(d.Facebook, "https://facebook.com/");
        LinkedIn = Social(d.LinkedIn, "https://linkedin.com/in/");
        Susep = Clean(d.Susep, 30);
        Cnpj = Clean(d.Cnpj, 20);
        var products = (d.Products ?? []).Select(p => p.Trim()).Where(p => p.Length > 0).Take(12).Select(p => p.Length > 80 ? p[..80] : p);
        ProductsText = Clean(string.Join('\n', products), 1100);
        AccentColor = color ?? DefaultAccent;
        Touch();
        return Result.Success();
    }

    /// <summary>"@corretora" ou "corretora" vira link completo; links completos ficam como estao.</summary>
    private static string? Social(string? value, string prefix)
    {
        var v = Clean(value, 200);
        if (v is null)
        {
            return null;
        }

        return v.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? v : prefix + v.TrimStart('@');
    }

    private static string? Clean(string? v, int max)
    {
        var t = v?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > max ? t[..max] : t;
    }

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$")]
    private static partial Regex HostPattern();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}

/// <summary>Imagem da home (logo ou capa), guardada no banco: entra no backup sem configurar nada.</summary>
public sealed class SiteImage : Entity
{
    public const int MaxBytes = 1_500_000;
    public static readonly string[] Kinds = ["logo", "cover"];

    private SiteImage() { } // EF

    public string Kind { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public byte[] Data { get; private set; } = [];

    public static Result<SiteImage> Create(string kind, byte[] data)
    {
        var image = new SiteImage { Kind = kind };
        var result = image.Replace(data);
        return result.IsFailure ? result.Error : image;
    }

    public Result Replace(byte[] data)
    {
        if (data.Length is 0 or > MaxBytes)
        {
            return Error.Validation("Site.ImageSize", "Imagem muito grande (máximo 1,5 MB).");
        }

        var type = Sniff(data);
        if (type is null)
        {
            return Error.Validation("Site.ImageType", "Use uma imagem PNG, JPG ou WebP.");
        }

        ContentType = type;
        Data = data;
        Touch();
        return Result.Success();
    }

    /// <summary>Tipo pelo conteudo (nao confia na extensao/nome enviado).</summary>
    private static string? Sniff(byte[] d) =>
        d.Length < 12 ? null
        : d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 ? "image/png"
        : d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF ? "image/jpeg"
        : d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P' ? "image/webp"
        : null;
}
