using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.Clients;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Policies;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Imports;
using Vitta.Domain.Policies;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Abstractions.Queries;

// Lado de LEITURA: projecoes direto para DTO (sem tracking), implementadas em Vitta.Persistence.

public interface IClientQueries
{
    Task<PagedResult<ClientListItemDto>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<ClientDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);
}

public interface IPolicyQueries
{
    Task<PagedResult<PolicyDto>> SearchAsync(PolicyFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<BookSummaryDto> SummaryAsync(CancellationToken ct = default);
}

public sealed record PolicyFilter(Guid? ClientId, Guid? InsurerId, PolicyStatus? Status, string? Search);

public interface IProspectQueries
{
    Task<IReadOnlyList<ProspectDto>> ListAsync(ProspectStage? stage, string? search, CancellationToken ct = default);
    Task<PagedResult<ProspectDto>> SearchAsync(ProspectFilter filter, ProspectSort sort, int page, int pageSize, DateOnly today, CancellationToken ct = default);
    Task<ProspectInsightsDto> InsightsAsync(ProspectFilter filter, DateOnly today, DateTimeOffset now, CancellationToken ct = default);
    Task<ProspectFilterOptionsDto> FilterOptionsAsync(CancellationToken ct = default);
}

public interface IPortfolioQueries
{
    Task<IReadOnlyList<PortfolioImportDto>> ListImportsAsync(CancellationToken ct = default);
    Task<PortfolioImportDetailDto?> GetImportAsync(Guid id, CancellationToken ct = default);
    /// <summary>Linhas da carteira do mes; nulo quando a importacao nao existe.</summary>
    Task<IReadOnlyList<PortfolioRowDto>?> ListRowsAsync(Guid importId, CancellationToken ct = default);
}

public interface IDashboardQueries
{
    Task<DashboardDto> GetAsync(DateOnly today, CancellationToken ct = default);
}

public interface IImportTemplateQueries
{
    Task<IReadOnlyList<ImportTemplateDto>> ListAsync(ImportTemplateKind? kind, CancellationToken ct = default);
    Task<ImportTemplateDto?> GetAsync(Guid id, CancellationToken ct = default);
}
