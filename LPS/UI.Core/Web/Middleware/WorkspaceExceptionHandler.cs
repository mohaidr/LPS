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
                Title = "The request has validation errors."
            };
        }
        else
        {
            var status = exception switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidOperationException => StatusCodes.Status409Conflict,
                ArgumentException => StatusCodes.Status400BadRequest,
                HttpRequestException => StatusCodes.Status502BadGateway,
                TimeoutException => StatusCodes.Status504GatewayTimeout,
                _ => 0
            };
            if (status == 0)
                return false;
            var title = exception switch
            {
                HttpRequestException => "The target API could not be reached or returned an invalid response.",
                TimeoutException => "The API request timed out.",
                _ => exception.Message
            };
            problem = new ProblemDetails { Status = status, Title = title };
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken: token);
        return true;
    }
}