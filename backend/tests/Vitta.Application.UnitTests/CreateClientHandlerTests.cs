using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Features.Clients.CreateClient;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Application.UnitTests;

public class CreateClientHandlerTests
{
    private readonly FakeClientRepository _repo = new();

    [Fact]
    public async Task Handle_NewClient_PersistsAndReturnsId()
    {
        var handler = new CreateClientHandler(_repo, _repo);

        var result = await handler.Handle(new CreateClientCommand("Ana Silva", "529.982.247-25", null, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Single(_repo.Clients);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Handle_DuplicateCpf_ReturnsConflict()
    {
        var handler = new CreateClientHandler(_repo, _repo);
        await handler.Handle(new CreateClientCommand("Ana", "52998224725", null, null, null), default);

        var result = await handler.Handle(new CreateClientCommand("Outra Ana", "529.982.247-25", null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.CpfAlreadyExists, result.Error);
    }

    private sealed class FakeClientRepository : IClientRepository, IUnitOfWork
    {
        public List<Client> Clients { get; } = [];
        public int SaveCount { get; private set; }

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clients.FirstOrDefault(c => c.Id == id));

        public Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clients.Any(c => c.Cpf == cpf));

        public Task<PagedResult<Client>> SearchAsync(string? term, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<Client>(Clients, page, pageSize, Clients.Count));

        public void Add(Client client) => Clients.Add(client);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(++SaveCount);
    }
}
