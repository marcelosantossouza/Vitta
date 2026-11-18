using Microsoft.EntityFrameworkCore;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Persistence.Repositories;

internal sealed class ClientRepository(VittaDbContext db) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken cancellationToken = default) =>
        db.Clients.AnyAsync(c => c.Cpf == cpf, cancellationToken);

    public async Task<PagedResult<Client>> SearchAsync(string? term, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern) || EF.Functions.ILike((string)(object)c.Cpf, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Client>(items, page, pageSize, total);
    }

    public void Add(Client client) => db.Clients.Add(client);
}
