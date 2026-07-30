namespace AgendadorContas.Services;

public static class SecurityHeadersMiddlewareExtensions
{
    private const string ContentSecurityPolicy = "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        bool includeHsts = false)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                ApplySecurityHeaders(context.Response.Headers, includeHsts);
                return Task.CompletedTask;
            });

            await next();
        });
    }

    public static void ApplySecurityHeaders(
        IHeaderDictionary headers,
        bool includeHsts = false)
    {
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
        headers.TryAdd("Content-Security-Policy", ContentSecurityPolicy);
        if (includeHsts)
        {
            headers.TryAdd(
                "Strict-Transport-Security",
                "max-age=31536000; includeSubDomains");
        }
    }
}
