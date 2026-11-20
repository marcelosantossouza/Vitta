using Vitta.Application.Features.Clients;
using Vitta.Domain.Clients;

namespace Vitta.Application.UnitTests;

public class ClientHandlerTests
{
    private readonly FakeClientRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task Create_NewClient_PersistsAndReturnsId()
    {
        var result = await new CreateClientHandler(_repo, _uow).Handle(new CreateClientCommand("Ana Silva", "529.982.247-25", null, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Single(_repo.Clients);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Create_DuplicateCpf_ReturnsConflict()
    {
        var handler = new CreateClientHandler(_repo, _uow);
        await handler.Handle(new CreateClientCommand("Ana", "52998224725", null, null, null), default);

        var result = await handler.Handle(new CreateClientCommand("Outra Ana", "529.982.247-25", null, null, null), default);

        Assert.Equal(ClientErrors.CpfAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Delete_ClientWithPolicies_ReturnsConflict()
    {
        var client = Client.Create("Ana", "52998224725", null, null, null).Value;
        _repo.Add(client);
        _repo.WithPolicies.Add(client.Id);

        var result = await new DeleteClientHandler(_repo, _uow).Handle(new DeleteClientCommand(client.Id), default);

        Assert.Equal(ClientErrors.HasPolicies, result.Error);
        Assert.Single(_repo.Clients);
    }
}
