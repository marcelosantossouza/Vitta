using Mediator;
using Vitta.API.Infrastructure;
using Vitta.Application.Features.Clients.CreateClient;
using Vitta.Application.Features.Clients.GetClientById;
using Vitta.Application.Features.Clients.GetClients;

namespace Vitta.API.Endpoints;

internal static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/clients").WithTags("Clientes");

        group.MapGet("/", async (string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetClientsQuery(search, page ?? 1, pageSize ?? 20), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetClientByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();
        }).WithName("GetClientById");

        group.MapPost("/", async (CreateClientCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetClientById", new { id = result.Value }, new { id = result.Value })
                : result.ToProblem();
        });

        return app;
    }
}
