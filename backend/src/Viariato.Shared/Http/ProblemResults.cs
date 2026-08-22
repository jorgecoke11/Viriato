using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace Viariato.Shared.Http;

public static class ProblemResults
{
    public static IResult Unauthorized(HttpContext http, string detail) => Results.Problem(
        type: "https://viariato.app/errors/unauthorized",
        title: "Unauthorized",
        statusCode: StatusCodes.Status401Unauthorized,
        detail: detail,
        instance: http.Request.Path);

    public static IResult Forbidden(HttpContext http, string detail) => Results.Problem(
        type: "https://viariato.app/errors/forbidden",
        title: "Forbidden",
        statusCode: StatusCodes.Status403Forbidden,
        detail: detail,
        instance: http.Request.Path);

    public static IResult NotFound(HttpContext http, string detail) => Results.Problem(
        type: "https://viariato.app/errors/not-found",
        title: "Not found",
        statusCode: StatusCodes.Status404NotFound,
        detail: detail,
        instance: http.Request.Path);

    public static IResult Conflict(HttpContext http, string detail) => Results.Problem(
        type: "https://viariato.app/errors/conflict",
        title: "Conflict",
        statusCode: StatusCodes.Status409Conflict,
        detail: detail,
        instance: http.Request.Path);

    public static IResult ValidationProblem(ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
}
