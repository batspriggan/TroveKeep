using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TroveKeep.Api.Filters;

/// <summary>
/// Global safety net for the project-wide error contract: <see cref="InvalidOperationException"/>
/// becomes 400 and <see cref="KeyNotFoundException"/> becomes 404, both with an
/// <c>{ "error": ... }</c> body. It exists because the per-controller <c>try/catch</c> blocks are
/// not exhaustive: for instance creating an entity while belonging to no collection throws from
/// the repository and used to surface as an unhandled 500.
///
/// Controllers that already catch these exceptions keep working unchanged: the action runs first
/// and its result wins, so this filter only sees what nothing else handled.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case InvalidOperationException ex:
                context.Result = new BadRequestObjectResult(new { error = ex.Message });
                context.ExceptionHandled = true;
                break;
            case KeyNotFoundException ex:
                context.Result = new NotFoundObjectResult(new { error = ex.Message });
                context.ExceptionHandled = true;
                break;
        }
    }
}
