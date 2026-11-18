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
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        });
}
