using Vitta.Domain.Common;

namespace Vitta.API.Infrastructure;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Result result) => Results.Problem(
        title: result.Error.Code,
        detail: result.Error.Message,
        statusCode: result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        });

    public static IResult ToOk<T>(this Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();

    public static IResult ToNoContent(this Result result) => result.IsSuccess ? Results.NoContent() : result.ToProblem();

    public static IResult ToCreated(this Result<Guid> result, string location, string idName = "id") =>
        result.IsSuccess
            ? Results.Created($"{location}/{result.Value}", new Dictionary<string, Guid> { [idName] = result.Value })
            : result.ToProblem();
}
