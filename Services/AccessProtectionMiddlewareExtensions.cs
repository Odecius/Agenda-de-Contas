using AgendadorContas.Options;
using Microsoft.Extensions.Options;

namespace AgendadorContas.Services;

public static class AccessProtectionMiddlewareExtensions
{
    public static IApplicationBuilder UseAccessProtection(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<AccessProtectionOptions>>().Value;
            if (!options.Enabled || IsAnonymousPath(context.Request.Path))
            {
                await next();
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                await next();
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.Redirect("/login.html");
        });
    }

    private static bool IsAnonymousPath(PathString path)
    {
        return path.Equals("/login.html", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/status", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }
}
