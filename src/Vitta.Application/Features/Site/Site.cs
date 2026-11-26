using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Settings;

namespace Vitta.Application.Features.Site;

// Home page publica da corretora. Nome, titulo, WhatsApp e o formulario de contato vem da pagina de captacao.

/// <param name="LogoVersion">Muda quando a imagem muda (vai na URL para furar o cache); nulo = sem imagem.</param>
/// <param name="LeadFormEnabled">Formulario de contato so aparece com a pagina de captacao ligada (consentimento).</param>
public sealed record SiteDto(
    bool Enabled, string? Host, string BrokerName, string Headline, string? WhatsApp,
    string? About, string? Address, string? Phone, string? Email, string? Hours,
    string? Instagram, string? Facebook, string? LinkedIn, string? Susep, string? Cnpj, IReadOnlyList<string> Products,
    string AccentColor, string? LogoVersion, string? CoverVersion, bool LeadFormEnabled, string ConsentText);

public sealed record GetSiteQuery : IQuery<SiteDto>;

public sealed class GetSiteHandler(IBrokerSiteRepository sites, ILeadCaptureSettingsRepository leadPage, ISiteImageRepository images)
    : IQueryHandler<GetSiteQuery, SiteDto>
{
    public async ValueTask<SiteDto> Handle(GetSiteQuery q, CancellationToken ct)
    {
        var s = await sites.GetAsync(ct) ?? BrokerSite.CreateDefault();
        var page = await leadPage.GetAsync(ct) ?? LeadCaptureSettings.CreateDefault();
        var versions = await images.VersionsAsync(ct);
        string? V(string kind) => versions.TryGetValue(kind, out var at) ? at.ToUnixTimeSeconds().ToString() : null;
        return new SiteDto(s.Enabled, s.Host, page.BrokerName, page.Headline, page.WhatsApp, s.About, s.Address, s.Phone, s.Email, s.Hours,
            s.Instagram, s.Facebook, s.LinkedIn, s.Susep, s.Cnpj, s.Products, s.AccentColor, V("logo"), V("cover"), page.Enabled, page.ConsentText);
    }
}

public sealed record UpdateSiteCommand(BrokerSiteData Data) : ICommand<Result>;

public sealed class UpdateSiteHandler(IBrokerSiteRepository sites, IUnitOfWork uow) : ICommandHandler<UpdateSiteCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateSiteCommand c, CancellationToken ct)
    {
        var current = await sites.GetAsync(ct);
        var site = current ?? BrokerSite.CreateDefault();
        var result = site.Update(c.Data);
        if (result.IsFailure)
        {
            return result;
        }

        if (current is null)
        {
            sites.Add(site);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record SiteImageDto(string ContentType, byte[] Data, DateTimeOffset Version);

public sealed record GetSiteImageQuery(string Kind) : IQuery<SiteImageDto?>;

public sealed class GetSiteImageHandler(ISiteImageRepository images) : IQueryHandler<GetSiteImageQuery, SiteImageDto?>
{
    public async ValueTask<SiteImageDto?> Handle(GetSiteImageQuery q, CancellationToken ct) =>
        await images.GetAsync(q.Kind, ct) is { } i ? new SiteImageDto(i.ContentType, i.Data, i.UpdatedAt ?? i.CreatedAt) : null;
}

/// <summary>Envia (ou troca) a logo/capa. Data nulo = remover.</summary>
public sealed record SaveSiteImageCommand(string Kind, byte[]? Data) : ICommand<Result>;

public sealed class SaveSiteImageHandler(ISiteImageRepository images, IUnitOfWork uow) : ICommandHandler<SaveSiteImageCommand, Result>
{
    public async ValueTask<Result> Handle(SaveSiteImageCommand c, CancellationToken ct)
    {
        if (!SiteImage.Kinds.Contains(c.Kind))
        {
            return Error.Validation("Site.ImageKind", "Tipo de imagem inválido.");
        }

        var current = await images.GetAsync(c.Kind, ct);
        if (c.Data is null)
        {
            if (current is not null)
            {
                images.Remove(current);
            }
        }
        else if (current is not null)
        {
            var replaced = current.Replace(c.Data);
            if (replaced.IsFailure)
            {
                return replaced;
            }
        }
        else
        {
            var created = SiteImage.Create(c.Kind, c.Data);
            if (created.IsFailure)
            {
                return created.Error;
            }

            images.Add(created.Value);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// O Caddy pergunta antes de emitir certificado para um endereco novo (TLS sob demanda):
/// so os enderecos cadastrados em Configuracoes ganham certificado (evita abuso com dominios aleatorios).
/// </summary>
public sealed record IsSiteDomainQuery(string? Domain) : IQuery<bool>;

public sealed class IsSiteDomainHandler(IBrokerSiteRepository sites) : IQueryHandler<IsSiteDomainQuery, bool>
{
    public async ValueTask<bool> Handle(IsSiteDomainQuery q, CancellationToken ct)
    {
        var domain = q.Domain?.Trim().TrimEnd('.').ToLowerInvariant();
        return !string.IsNullOrEmpty(domain) && await sites.GetAsync(ct) is { Host: { } host } && host == domain;
    }
}
