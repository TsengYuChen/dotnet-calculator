namespace VehicleGo.Web.Startup;

/// <summary>
/// Serves the Vue build's <c>index.html</c> for client-side routes. <c>/api</c> and <c>/signin-oidc</c>
/// paths are never answered with the SPA: they get a 404 ProblemDetails instead.
/// </summary>
public static class SpaFallback
{
    private static readonly string[] ExcludedPrefixes = ["/api", "/signin-oidc"];

    public static bool IsExcluded(PathString path) =>
        ExcludedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    public static async Task HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var index = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider.GetFileInfo("index.html");
        if (IsExcluded(context.Request.Path) || !index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = { Status = StatusCodes.Status404NotFound },
            });
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(index);
    }
}
