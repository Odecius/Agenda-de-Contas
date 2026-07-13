namespace AgendadorContas.Services;

public static class SecurityHeadersMiddlewareExtensions
{
    private const string ContentSecurityPolicy = "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("Referrer-Policy", "no-referrer");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
                headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
                headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
                headers.TryAdd("Content-Security-Policy", ContentSecurityPolicy);
                return Task.CompletedTask;
            });

            await next();
        });
    }
}
