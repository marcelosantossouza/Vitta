using Mediator;
using Vitta.Application.Features.Insurers.GetInsurers;

namespace Vitta.API.Endpoints;

internal static class InsurerEndpoints
{
    public static IEndpointRouteBuilder MapInsurerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/insurers", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetInsurersQuery(), ct)))
            .WithTags("Seguradoras");
        return app;
    }
}
