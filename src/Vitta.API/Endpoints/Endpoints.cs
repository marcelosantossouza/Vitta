using Mediator;
using Microsoft.AspNetCore.Mvc;
using Vitta.API.Infrastructure;
using Vitta.Application.Features.Auth;
using Vitta.Application.Features.Clients;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Insurers;
using Vitta.Application.Features.Policies;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Application.Features.Settings;
using Vitta.Domain.Imports;
using Vitta.Domain.Policies;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;

namespace Vitta.API.Endpoints;

// Endpoints "magros": so traduzem HTTP <-> command/query. Regras ficam na Application/Domain.

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("Autenticacao");
        g.MapPost("/login", async (LoginCommand cmd, ISender s, CancellationToken ct) => (await s.Send(cmd, ct)).ToOk()).AllowAnonymous();
        g.MapGet("/me", async (ISender s, CancellationToken ct) => (await s.Send(new GetCurrentUserQuery(), ct)).ToOk()).RequireAuthorization();
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
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteClientCommand(id), ct)).ToNoContent());
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
    public sealed record ProspectInput(string Name, string? Phone, string? Email, string? Source, string? Notes, DateOnly? NextFollowUpOn);
    public sealed record StageInput(ProspectStage Stage, DateOnly? NextFollowUpOn);
    public sealed record ConvertInput(string Cpf, DateOnly? BirthDate);

    public static void MapProspectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/prospects").WithTags("Prospeccao");
        g.MapGet("/", async (ProspectStage? stage, string? search, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new GetProspectsQuery(stage, search), ct)));
        g.MapPost("/", async (ProspectInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateProspectCommand(i.Name, i.Phone, i.Email, i.Source, i.Notes, i.NextFollowUpOn), ct)).ToCreated("/api/prospects"));
        g.MapPut("/{id:guid}", async (Guid id, ProspectInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateProspectCommand(id, i.Name, i.Phone, i.Email, i.Source, i.Notes, i.NextFollowUpOn), ct)).ToNoContent());
        g.MapPost("/{id:guid}/stage", async (Guid id, StageInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new MoveProspectStageCommand(id, i.Stage, i.NextFollowUpOn), ct)).ToNoContent());
        g.MapPost("/{id:guid}/convert", async (Guid id, ConvertInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new ConvertProspectCommand(id, i.Cpf, i.BirthDate), ct)).ToCreated("/api/clients", "clientId"));
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteProspectCommand(id), ct)).ToNoContent());

        g.MapPost("/imports", async ([FromForm] Guid templateId, IFormFile file, ISender s, CancellationToken ct) =>
        {
            if (Uploads.Check(file) is { } problem)
            {
                return problem;
            }

            await using var stream = await Uploads.ToMemoryAsync(file, ct);
            return (await s.Send(new ImportProspectsCommand(templateId, file.FileName, stream), ct)).ToOk();
        }).DisableAntiforgery();
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
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeletePortfolioImportCommand(id), ct)).ToNoContent());

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
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteImportTemplateCommand(id), ct)).ToNoContent());

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

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/settings").WithTags("Configuracoes");
        g.MapGet("/ai", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetAiSettingsQuery(), ct)));
        g.MapPut("/ai", async (AiSettingsInput i, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateAiSettingsCommand(i.Provider, i.Model, i.Effort), ct)).ToNoContent());
        g.MapPost("/ai/test", async (AiSettingsInput i, ISender s, CancellationToken ct) =>
            Results.Ok(await s.Send(new TestAiConnectionCommand(i.Provider, i.Model, i.Effort), ct)));
    }
}
