using Vitta.Application.Features.Prospects;
using Vitta.Domain.Prospects;

namespace Vitta.Application.UnitTests;

public class ConvertProspectHandlerTests
{
    private readonly FakeProspectRepository _prospects = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeUnitOfWork _uow = new();

    [Fact]
    public async Task Convert_CreatesClientWithProspectDataAndMarksWon()
    {
        var prospect = Prospect.Create("Carla Souza", "(21) 90000-0000", "carla@ex.com", "Indicacao").Value;
        _prospects.Add(prospect);

        var result = await new ConvertProspectHandler(_prospects, _clients, _uow).Handle(new ConvertProspectCommand(prospect.Id, "529.982.247-25", null), default);

        Assert.True(result.IsSuccess);
        var client = Assert.Single(_clients.Clients);
        Assert.Equal("Carla Souza", client.FullName);
        Assert.Equal("carla@ex.com", client.Email);
        Assert.Equal(ProspectStage.Won, prospect.Stage);
        Assert.Equal(client.Id, prospect.ConvertedClientId);
    }

    [Fact]
    public async Task Convert_Twice_ReturnsConflict()
    {
        var prospect = Prospect.Create("Carla", null, null, null).Value;
        _prospects.Add(prospect);
        var handler = new ConvertProspectHandler(_prospects, _clients, _uow);
        await handler.Handle(new ConvertProspectCommand(prospect.Id, "52998224725", null), default);

        var second = await handler.Handle(new ConvertProspectCommand(prospect.Id, "11144477735", null), default);

        Assert.Equal(ProspectErrors.AlreadyConverted, second.Error);
        Assert.Single(_clients.Clients);
    }
}
