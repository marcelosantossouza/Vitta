using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Application.Features.Clients.GetClientById;

public sealed record GetClientByIdQuery(Guid Id) : IQuery<Result<ClientDto>>;

public sealed class GetClientByIdHandler(IClientRepository clients) : IQueryHandler<GetClientByIdQuery, Result<ClientDto>>
{
    public async ValueTask<Result<ClientDto>> Handle(GetClientByIdQuery query, CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(query.Id, cancellationToken);
        return client is null ? ClientErrors.NotFound(query.Id) : ClientDto.From(client);
    }
}
