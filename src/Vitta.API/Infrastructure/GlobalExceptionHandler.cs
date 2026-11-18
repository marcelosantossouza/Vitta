using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Vitta.API.Infrastructure;

internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is ValidationException validation)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            problem = new HttpValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Title = "Dados invalidos",
                Status = StatusCodes.Status400BadRequest,
            };
        }
        else
        {
            logger.LogError(exception, "Erro nao tratado");
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            problem = new ProblemDetails { Title = "Erro inesperado", Status = StatusCodes.Status500InternalServerError };
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
