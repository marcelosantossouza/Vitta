using Mediator;
using Vitta.Application.Abstractions.Persistence;

namespace Vitta.Application.Features.Clients.GetClients;

public sealed record GetClientsQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<PagedResult<ClientDto>>;

public sealed class GetClientsHandler(IClientRepository clients) : IQueryHandler<GetClientsQuery, PagedResult<ClientDto>>
{
    public async ValueTask<PagedResult<ClientDto>> Handle(GetClientsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var result = await clients.SearchAsync(query.Search, page, pageSize, cancellationToken);
        return new PagedResult<ClientDto>(result.Items.Select(ClientDto.From).ToList(), result.Page, result.PageSize, result.TotalCount);
    }
}
