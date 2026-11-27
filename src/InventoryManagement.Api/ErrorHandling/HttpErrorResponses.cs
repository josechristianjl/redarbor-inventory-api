using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace InventoryManagement.Api.ErrorHandling;

internal static class HttpErrorResponses
{
    private static readonly string[] InvalidValueMessages =
        ["The supplied value is invalid."];

    internal static IActionResult CreateInvalidModelStateResponse(
        ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = CreateProblem(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            "InvalidRequest",
            "The request contains invalid input.");
        problem.Extensions["errors"] = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                _ => InvalidValueMessages);

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    internal static Task WriteStatusCodeAsync(
        StatusCodeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var httpContext = context.HttpContext;
        var status = httpContext.Response.StatusCode;
        var problem = CreateProblem(
            httpContext,
            status,
            GetErrorCode(status),
            GetErrorDetail(status));

        return httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: httpContext.RequestAborted);
    }

    private static string GetErrorCode(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "InvalidRequest",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status405MethodNotAllowed => "MethodNotAllowed",
            StatusCodes.Status415UnsupportedMediaType => "UnsupportedMediaType",
            StatusCodes.Status500InternalServerError => "InternalServerError",
            _ => "HttpError"
        };
    }

    private static string GetErrorDetail(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest =>
                "The request contains invalid input.",
            StatusCodes.Status401Unauthorized =>
                "Authentication is required.",
            StatusCodes.Status403Forbidden =>
                "The requested operation is not permitted.",
            StatusCodes.Status404NotFound =>
                "The requested resource was not found.",
            StatusCodes.Status405MethodNotAllowed =>
                "The HTTP method is not supported for this resource.",
            StatusCodes.Status415UnsupportedMediaType =>
                "The supplied content type is not supported.",
            >= 500 => "An unexpected error occurred.",
            _ => "The request could not be completed."
        };
    }

    private static ProblemDetails CreateProblem(
        HttpContext context,
        int status,
        string code,
        string detail)
    {
        var title = ReasonPhrases.GetReasonPhrase(status);
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Status = status,
            Title = string.IsNullOrEmpty(title) ? "HTTP error" : title,
            Detail = detail,
            Instance = context.Request.Path.Value
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return problem;
    }
}
