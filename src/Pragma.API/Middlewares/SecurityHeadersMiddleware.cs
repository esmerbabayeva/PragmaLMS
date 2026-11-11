namespace Pragma.API.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // CSP icazələri: Google Auth, YouTube iframeləri, Jitsi Meet və CDN resursları
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://accounts.google.com https://meet.jit.si https://www.youtube.com https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://unpkg.com; " +
                  "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                  "font-src 'self' https://fonts.gstatic.com; " +
                  "frame-src 'self' https://accounts.google.com https://meet.jit.si https://www.youtube.com; " +
                  "img-src 'self' data: https:; " +
                  "connect-src 'self' https://accounts.google.com https://meet.jit.si https://unpkg.com https://cdn.jsdelivr.net;";

        context.Response.Headers.Append("Content-Security-Policy", csp);

        await _next(context);
    }
}
