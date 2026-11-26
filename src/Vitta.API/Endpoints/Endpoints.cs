using Mediator;
using Microsoft.AspNetCore.Mvc;
using Vitta.API.Infrastructure;
using Vitta.Application.Features.Auth;
using Vitta.Application.Features.Clients;
using Vitta.Application.Features.Contacts;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Insurers;
using Vitta.Application.Features.LeadCapture;
using Vitta.Application.Features.Notifications;
using Vitta.Application.Features.Policies;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Application.Features.Referrals;
using Vitta.Application.Features.Settings;
using Vitta.Application.Features.Site;
using Vitta.Application.Features.Users;
using Vitta.Domain.Contacts;
using Vitta.Domain.Imports;
using Vitta.Domain.Policies;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;
using Vitta.Domain.Users;

namespace Vitta.API.Endpoints;

// Endpoints "magros": so traduzem HTTP <-> command/query. Regras ficam na Application/Domain.

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("Autenticacao");
        g.MapPost("/login", async (LoginCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToOk()).AllowAnonymous();
        g.MapGet("/me", async (ISender s, CancellationToken ct) => (await s.Send(new GetCurrentUserQuery(), ct)).ToOk()).RequireAuthorization();
        g.MapPost("/change-password", async (ChangePasswordCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToNoContent())
            .RequireAuthorization();
    }
}

internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/dashboard", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetDashboardQuery(), ct))).WithTags("Painel");
}

internal static class ClientEndpoints
{
    public sealed record ClientInput(string FullName, DateOnly? BirthDate, string? Email, string? Phone, string? Notes);

    public static void MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/clients").WithTags("Clientes");
        g.MapGet("/", async (string? search, int? page, int? pageSize, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetClientsQuery(search, page ?? 1, pageSize ?? 20), ct)));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetClientByIdQuery(id), ct)).ToOk());
        g.MapPost("/", async (CreateClientCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToCreated("/api/clients"));
        g.MapPut("/{id:guid}", async (Guid id, ClientInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateClientCommand(id, i.FullName, i.BirthDate, i.Email, i.Phone, i.Notes), ct)).ToNoContent());
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteClientCommand(id), ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.CanDelete);
    }
}

internal static class PolicyEndpoints
{
    public sealed record PolicyUpdateInput(string ProductName, decimal MonthlyPremium, decimal? CommissionRate);
    public sealed record PolicyEndInput(PolicyStatus Status, DateOnly EndedOn, string? Reason);

    public static void MapPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/policies").WithTags("Apolices");
        g.MapGet("/summary", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetBookSummaryQuery(), ct)));
        g.MapGet("/", async (Guid? clientId, Guid? insurerId, PolicyStatus? status, string? search, int? page, int? pageSize, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetPoliciesQuery(clientId, insurerId, status, search, page ?? 1, pageSize ?? 20), ct)));
        g.MapPost("/", async (CreatePolicyCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToCreated("/api/policies"));
        g.MapPut("/{id:guid}", async (Guid id, PolicyUpdateInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdatePolicyCommand(id, i.ProductName, i.MonthlyPremium, i.CommissionRate), ct)).ToNoContent());
        g.MapPost("/{id:guid}/end", async (Guid id, PolicyEndInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new EndPolicyCommand(id, i.Status, i.EndedOn, i.Reason), ct)).ToNoContent());
        g.MapPost("/{id:guid}/reactivate", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new ReactivatePolicyCommand(id), ct)).ToNoContent());
    }
}

internal static class ProspectEndpoints
{
    public sealed record ProspectInput(
        string Name, string? Phone, string? Email, string? Source, string? Notes, DateOnly? NextFollowUpOn, ProspectProfile? Profile = null);
    public sealed record StageInput(ProspectStage Stage, DateOnly? NextFollowUpOn);
    public sealed record ConvertInput(string Cpf, DateOnly? BirthDate);
    public sealed record BulkStageInput(IReadOnlyList<Guid> Ids, ProspectStage Stage, DateOnly? NextFollowUpOn);
    public sealed record BulkIdsInput(IReadOnlyList<Guid> Ids);

    /// <summary>Filtros pela URL (?city=...&amp;stage=New&amp;stage=Contacted...). Mesmos para a lista e o dashboard.</summary>
    public sealed class FilterQuery
    {
        public string? Search { get; init; }
        public ProspectStage[]? Stage { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Source { get; init; }

        /// <summary>Id do lote, ou "none" = sem lote (importacoes antigas e cadastros manuais).</summary>
        public string? ImportId { get; init; }
        public decimal? SalaryMin { get; init; }
        public decimal? SalaryMax { get; init; }
        public FollowUpFilter? FollowUp { get; init; }
        public bool? HasPhone { get; init; }
        public LeadKind? Kind { get; init; }
        public string? CompanySize { get; init; }

        /// <summary>CPF ou CNPJ (comeca com estes digitos).</summary>
        public string? Document { get; init; }

        public ProspectFilter ToFilter() =>
            new(Search, Stage, City, State, Source, Guid.TryParse(ImportId, out var id) ? id : null, SalaryMin, SalaryMax,
                FollowUp ?? FollowUpFilter.Any, HasPhone, WithoutImport: ImportId == "none", Kind: Kind ?? LeadKind.Any,
                CompanySize: CompanySize, Document: Document);
    }

    public static void MapProspectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/prospects").WithTags("Prospeccao");
        g.MapGet("/", async (ProspectStage? stage, string? search, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetProspectsQuery(stage, search), ct)));
        g.MapGet("/search", async ([AsParameters] FilterQuery f, ProspectSort? sort, int? page, int? pageSize, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new SearchProspectsQuery(f.ToFilter(), sort ?? ProspectSort.FollowUp, page ?? 1, pageSize ?? 50), ct)));
        g.MapGet("/board", async ([AsParameters] FilterQuery f, int? take, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetProspectBoardQuery(f.ToFilter(), take ?? 30), ct)));
        g.MapGet("/insights", async ([AsParameters] FilterQuery f, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetProspectInsightsQuery(f.ToFilter()), ct)));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            await s.Send(new GetProspectQuery(id), ct) is { } p ? Results.Ok(p) : Results.NotFound());
        g.MapGet("/filter-options", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetProspectFilterOptionsQuery(), ct)));
        g.MapGet("/ids", async ([AsParameters] FilterQuery f, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetProspectIdsQuery(f.ToFilter()), ct)));
        g.MapPost("/bulk-delete", async (BulkIdsInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new BulkDeleteProspectsCommand(i.Ids), ct)).ToOk())
            .RequireAuthorization(AuthPolicies.CanDelete);
        g.MapPost("/bulk-stage", async (BulkStageInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new BulkMoveProspectsCommand(i.Ids, i.Stage, i.NextFollowUpOn), ct)).ToOk());
        g.MapPost("/", async (ProspectInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateProspectCommand(i.Name, i.Phone, i.Email, i.Source, i.Notes, i.NextFollowUpOn, i.Profile), ct)).ToCreated("/api/prospects"));
        g.MapPut("/{id:guid}", async (Guid id, ProspectInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateProspectCommand(id, i.Name, i.Phone, i.Email, i.Source, i.Notes, i.NextFollowUpOn, i.Profile), ct)).ToNoContent());
        g.MapPost("/{id:guid}/stage", async (Guid id, StageInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new MoveProspectStageCommand(id, i.Stage, i.NextFollowUpOn), ct)).ToNoContent());
        g.MapPost("/{id:guid}/convert", async (Guid id, ConvertInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new ConvertProspectCommand(id, i.Cpf, i.BirthDate), ct)).ToCreated("/api/clients", "clientId"));
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteProspectCommand(id), ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.CanDelete);

        g.MapPost("/imports", async ([FromForm] Guid templateId, [FromForm] bool? updateExisting, IFormFile file, ISender s, CancellationToken ct) =>
        {
            if (Uploads.Check(file) is { } problem)
            {
                return problem;
            }

            await using var stream = await Uploads.ToMemoryAsync(file, ct);
            return (await s.Send(new ImportProspectsCommand(templateId, file.FileName, stream, updateExisting ?? false), ct)).ToOk();
        }).DisableAntiforgery();
        g.MapDelete("/imports/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteProspectImportCommand(id), ct)).ToOk())
            .RequireAuthorization(AuthPolicies.CanDelete);
    }
}

internal static class Uploads
{
    private const long MaxBytes = 20 * 1024 * 1024;

    public static IResult? Check(IFormFile? file) =>
        file is null || file.Length is 0 or > MaxBytes
            ? Results.Problem(title: "Upload.InvalidFile", detail: "Envie um arquivo de até 20 MB.", statusCode: 400)
            : null;

    public static async Task<MemoryStream> ToMemoryAsync(IFormFile file, CancellationToken ct)
    {
        var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        return stream;
    }
}

internal static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/portfolio/imports").WithTags("Carteira");
        g.MapGet("/", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetPortfolioImportsQuery(), ct)));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetPortfolioImportQuery(id), ct)).ToOk());
        g.MapGet("/{id:guid}/rows", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetPortfolioImportRowsQuery(id), ct)).ToOk());
        g.MapPost("/{id:guid}/sync-book", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new SyncBookFromImportCommand(id), ct)).ToOk());
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeletePortfolioImportCommand(id), ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.CanDelete);

        g.MapPost("/", async ([FromForm] Guid? insurerId, [FromForm] string referenceMonth, [FromForm] Guid? templateId, IFormFile file, ISender s, CancellationToken ct) =>
        {
            if (!DateOnly.TryParseExact(referenceMonth + "-01", "yyyy-MM-dd", out var month))
            {
                return Results.Problem(title: "Portfolio.InvalidMonth", detail: "Mês de referência inválido (use aaaa-mm).", statusCode: 400);
            }

            if (Uploads.Check(file) is { } problem)
            {
                return problem;
            }

            await using var stream = await Uploads.ToMemoryAsync(file, ct);
            var result = await s.Send(new ImportPortfolioCommand(insurerId, month, file.FileName, stream, templateId), ct);
            return result.IsSuccess ? Results.Created($"/api/portfolio/imports/{result.Value.Id}", result.Value) : result.ToProblem();
        }).DisableAntiforgery();
    }
}

internal static class InsurerEndpoints
{
    public static void MapInsurerEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/insurers").WithTags("Seguradoras");
        g.MapGet("/", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetInsurersQuery(), ct)));
        g.MapPost("/", async (CreateInsurerCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToCreated("/api/insurers"));
    }
}

internal static class ImportTemplateEndpoints
{
    public sealed record ImportTemplateInput(
        string Name, ImportTemplateKind Kind, Guid? InsurerId, string? SheetName, int HeaderRow,
        IReadOnlyList<ColumnMapping>? Columns, bool? CreatedByAi, string? AiNotes);

    public static void MapImportTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/import-templates").WithTags("Modelos de planilha");
        g.MapGet("/", async (ImportTemplateKind? kind, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetImportTemplatesQuery(kind), ct)));
        g.MapGet("/fields", async (ImportTemplateKind kind, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetImportFieldsQuery(kind), ct)));
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetImportTemplateQuery(id), ct)).ToOk());
        g.MapPost("/", async (ImportTemplateInput i, ISender s, CancellationToken ct) =>
            (await s.Send(ToCommand(null, i), ct)).ToCreated("/api/import-templates"));
        g.MapPut("/{id:guid}", async (Guid id, ImportTemplateInput i, ISender s, CancellationToken ct) =>
            (await s.Send(ToCommand(id, i), ct)).ToNoContent());
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteImportTemplateCommand(id), ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.CanDelete);

        // Le uma planilha de exemplo e pede para a camada de IA sugerir o modelo (nada e gravado aqui).
        g.MapPost("/analyze", async ([FromForm] ImportTemplateKind kind, [FromForm] string? sheetName, IFormFile file, ISender s, CancellationToken ct) =>
        {
            if (Uploads.Check(file) is { } problem)
            {
                return problem;
            }

            await using var stream = await Uploads.ToMemoryAsync(file, ct);
            return (await s.Send(new AnalyzeSpreadsheetCommand(kind, file.FileName, stream, sheetName), ct)).ToOk();
        }).DisableAntiforgery();
    }

    private static SaveImportTemplateCommand ToCommand(Guid? id, ImportTemplateInput i) =>
        new(id, i.Name, i.Kind, i.InsurerId, i.SheetName, i.HeaderRow, i.Columns ?? [], i.CreatedByAi ?? false, i.AiNotes);
}

internal static class SettingsEndpoints
{
    public sealed record AiSettingsInput(AiProvider Provider, string Model, string Effort);
    public sealed record NotificationsInput(bool DailyEmailEnabled, IReadOnlyList<string>? Recipients, string SendAt);
    public sealed record LeadPageInput(bool Enabled, string? BrokerName, string? Headline, string? WhatsApp);
    public sealed record SiteInput(
        bool Enabled, string? Host, string? About, string? Address, string? Phone, string? Email, string? Hours,
        string? Instagram, string? Facebook, string? LinkedIn, string? Susep, string? Cnpj, IReadOnlyList<string>? Products, string? AccentColor);

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/settings").WithTags("Configuracoes").RequireAuthorization(AuthPolicies.Admin);
        g.MapGet("/ai", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetAiSettingsQuery(), ct)));
        g.MapPut("/ai", async (AiSettingsInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateAiSettingsCommand(i.Provider, i.Model, i.Effort), ct)).ToNoContent());
        g.MapPost("/ai/test", async (AiSettingsInput i, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new TestAiConnectionCommand(i.Provider, i.Model, i.Effort), ct)));

        g.MapGet("/lead-page", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetLeadCaptureSettingsQuery(), ct)));
        g.MapPut("/lead-page", async (LeadPageInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateLeadCaptureSettingsCommand(i.Enabled, i.BrokerName, i.Headline, i.WhatsApp), ct)).ToNoContent());

        g.MapGet("/site", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetSiteQuery(), ct)));
        g.MapPut("/site", async (SiteInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateSiteCommand(new(i.Enabled, i.Host, i.About, i.Address, i.Phone, i.Email, i.Hours, i.Instagram, i.Facebook,
                i.LinkedIn, i.Susep, i.Cnpj, i.Products, i.AccentColor)), ct)).ToNoContent());
        g.MapPut("/site/images/{kind}", async (string kind, IFormFile file, ISender s, CancellationToken ct) =>
        {
            if (file is null || file.Length is 0 or > Domain.Settings.SiteImage.MaxBytes)
            {
                return Results.Problem(title: "Site.ImageSize", detail: "Imagem muito grande (máximo 1,5 MB).", statusCode: 400);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return (await s.Send(new SaveSiteImageCommand(kind, ms.ToArray()), ct)).ToNoContent();
        }).DisableAntiforgery();
        g.MapDelete("/site/images/{kind}", async (string kind, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveSiteImageCommand(kind, null), ct)).ToNoContent());

        g.MapGet("/notifications", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetNotificationSettingsQuery(), ct)));
        g.MapPut("/notifications", async (NotificationsInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateNotificationSettingsCommand(i.DailyEmailEnabled, i.Recipients ?? [], i.SendAt), ct)).ToNoContent());
        g.MapPost("/notifications/send-now", async (ISender s, CancellationToken ct) =>
            (await s.Send(new SendDailyDigestCommand(Scheduled: false), ct)).ToOk());
    }
}

internal static class ContactEndpoints
{
    public sealed record ContactInput(
        Guid? ProspectId, Guid? ClientId, ContactChannel Channel, string Summary, DateTimeOffset? OccurredAt, DateOnly? NextFollowUpOn, bool? AdvanceStage);

    public static void MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/contacts").WithTags("Contatos");
        g.MapGet("/", async (Guid? prospectId, Guid? clientId, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetContactsQuery(prospectId, clientId), ct)));
        g.MapPost("/", async (ContactInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new RegisterContactCommand(i.ProspectId, i.ClientId, i.Channel, i.Summary, i.OccurredAt, i.NextFollowUpOn, i.AdvanceStage ?? false), ct))
                .ToCreated("/api/contacts"));
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteContactCommand(id), ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.CanDelete);
    }
}

internal static class ReferralEndpoints
{
    public sealed record ReferralInput(string Name, string? Code, string? Notes, decimal? SharePercent, string? ShareLabel);
    public sealed record AssignInput(Guid? GroupId, IReadOnlyList<Guid> ClientIds);

    /// <summary>Grupos de indicacao: so o dono (Administrador) carimba e ve os resultados.</summary>
    public static void MapReferralEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/referrals").WithTags("Indicacoes").RequireAuthorization(AuthPolicies.Admin);
        g.MapGet("/", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetReferralGroupsQuery(), ct)));
        g.MapGet("/{id:guid}/report", async (Guid id, DateOnly? month, ISender s, CancellationToken ct) =>
            (await s.Send(new GetReferralReportQuery(id, month), ct)).ToOk());
        g.MapPost("/", async (ReferralInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveReferralGroupCommand(null, i.Name, i.Code, i.Notes, i.SharePercent, i.ShareLabel), ct)).ToCreated("/api/referrals"));
        g.MapPut("/{id:guid}", async (Guid id, ReferralInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveReferralGroupCommand(id, i.Name, i.Code, i.Notes, i.SharePercent, i.ShareLabel), ct)).ToNoContent());
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteReferralGroupCommand(id), ct)).ToNoContent());
        g.MapPost("/assign", async (AssignInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new AssignReferralCommand(i.GroupId, i.ClientIds ?? []), ct)).ToOk());
    }
}

internal static class UserEndpoints
{
    public sealed record CreateUserInput(string Name, string Email, UserRole Role, string TemporaryPassword);
    public sealed record UpdateUserInput(string Name, UserRole Role);
    public sealed record PasswordInput(string TemporaryPassword);

    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/users").WithTags("Usuarios").RequireAuthorization(AuthPolicies.Admin);
        g.MapGet("/", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetUsersQuery(), ct)));
        g.MapPost("/", async (CreateUserInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateUserCommand(i.Name, i.Email, i.Role, i.TemporaryPassword), ct)).ToCreated("/api/users"));
        g.MapPut("/{id:guid}", async (Guid id, UpdateUserInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateUserCommand(id, i.Name, i.Role), ct)).ToNoContent());
        g.MapPost("/{id:guid}/activate", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new SetUserActiveCommand(id, true), ct)).ToNoContent());
        g.MapPost("/{id:guid}/deactivate", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new SetUserActiveCommand(id, false), ct)).ToNoContent());
        g.MapPost("/{id:guid}/reset-password", async (Guid id, PasswordInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new ResetUserPasswordCommand(id, i.TemporaryPassword), ct)).ToNoContent());
    }
}

/// <summary>Rotas publicas (sem login): pagina de captacao. Limitadas por IP contra spam.</summary>
internal static class PublicEndpoints
{
    public const string RateLimitPolicy = "public-leads";

    public sealed record LeadInput(
        string Name, string Phone, string? Email, string? City, string? State, DateOnly? BirthDate, string? Interest,
        string? Origin, bool Consent, string? Website);

    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/public").WithTags("Publico").AllowAnonymous();
        g.MapGet("/lead-page", async (ISender s, CancellationToken ct) =>
        {
            var page = await s.Send(new GetLeadPageQuery(), ct);
            return page.Enabled ? Results.Ok(page) : Results.NotFound();
        });
        g.MapGet("/site", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetSiteQuery(), ct)));
        g.MapGet("/site/domain-check", async (string? domain, ISender s, CancellationToken ct) =>
            await s.Send(new IsSiteDomainQuery(domain), ct) ? Results.Ok() : Results.NotFound());
        g.MapGet("/site/images/{kind}", async (string kind, HttpContext http, ISender s, CancellationToken ct) =>
        {
            if (await s.Send(new GetSiteImageQuery(kind), ct) is not { } image)
            {
                return Results.NotFound();
            }

            // A URL leva ?v=<versao>: pode ficar em cache "para sempre"; trocar a imagem muda a URL.
            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(image.Data, image.ContentType);
        });
        g.MapPost("/leads", async (LeadInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitLeadCommand(i.Name, i.Phone, i.Email, i.City, i.State, i.BirthDate, i.Interest, i.Origin, i.Consent, i.Website), ct))
                .ToNoContent())
            .RequireRateLimiting(RateLimitPolicy);
    }
}
