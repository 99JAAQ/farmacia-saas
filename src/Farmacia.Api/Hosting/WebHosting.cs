namespace Farmacia.Api.Hosting;

/// <summary>
/// The React app and the API on the same origin (one container): the SameSite=Strict refresh cookie, the SignalR
/// connection and the absence of CORS all depend on it. The build of web/ is copied to wwwroot by the Dockerfile; in
/// development there is no wwwroot and Vite serves the app with its proxy, which gives the same single origin.
/// </summary>
public static class WebHosting
{
    /// <summary>
    /// Client-side routes (/caja, /catalogo/123) get index.html, so a reload or a shared link works. Everything under
    /// /api, /hubs and /health is left alone: an unknown API route must answer 404, not a web page.
    /// </summary>
    public const string SpaFallbackPattern = "{*path:regex(^(?!api/|api$|hubs/|health$|openapi/|scalar).*$)}";

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (!app.Environment.IsDevelopment())
            {
                // Only what the app uses, from its own origin. Inline styles: React's style={{...}} attributes (the
                // charts' bar heights). connect-src 'self' covers the API and the hub's WebSocket on the same host.
                headers.ContentSecurityPolicy =
                    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
                    "font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            }

            await next(context);
        });
        return app;
    }

    public static WebApplication MapSpa(this WebApplication app)
    {
        // Public on purpose: the login page itself is part of it. The data stays behind the API's policies.
        app.MapFallbackToFile(SpaFallbackPattern, "index.html").AllowAnonymous();
        return app;
    }
}
