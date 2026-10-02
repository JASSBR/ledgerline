using Ledgerline.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Ledgerline.Hosting;

/// <summary>Business failures → RFC 9457 problem details with a stable machine-readable code.</summary>
public static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Type == ErrorType.Validation)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { [error.Code] = [error.Description] },
                title: error.Description);
        }

        return TypedResults.Problem(
            statusCode: error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status409Conflict,
            },
            title: error.Description,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code });
    }
}
