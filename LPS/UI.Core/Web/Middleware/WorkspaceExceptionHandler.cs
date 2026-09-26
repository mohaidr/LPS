using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LPS.UI.Core.Web.Middleware;

internal sealed class WorkspaceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        ProblemDetails problem;
        if (exception is ValidationException validation)
        {
            problem = new ValidationProblemDetails(validation.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The plan has validation errors."
            };
        }
        else
        {
            var status = exception switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidOperationException => StatusCodes.Status409Conflict,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => 0
            };
            if (status == 0)
                return false;
            problem = new ProblemDetails { Status = status, Title = exception.Message };
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken: token);
        return true;
    }
}