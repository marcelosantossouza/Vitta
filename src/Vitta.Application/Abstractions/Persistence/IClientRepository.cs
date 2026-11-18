using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Application.Abstractions.Persistence;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken cancellationToken = default);
    Task<PagedResult<Client>> SearchAsync(string? term, int page, int pageSize, CancellationToken cancellationToken = default);
    void Add(Client client);
}
