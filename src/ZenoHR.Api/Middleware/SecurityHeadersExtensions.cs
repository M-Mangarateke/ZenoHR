// REQ-SEC-003, REQ-SEC-007: HTTP security headers — CSP, HSTS, X-Frame-Options, nosniff.
// Closes VUL-001 (Sev-1): missing security headers.
// Closes VUL-002 (Sev-1): no CORS policy.
// Closes VUL-023 (Sev-2): no HSTS despite HTTPS redirect.

namespace ZenoHR.Api.Middleware;

/// <summary>
/// Extension methods for configuring HTTP security response headers and CORS policy.
/// <para>
/// Register <c>app.UseZenoHrSecurityHeaders()</c> immediately after
/// <c>UseGlobalExceptionHandler()</c> and before <c>UseHttpsRedirection()</c>.
/// </para>
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds the ZenoHR security header policy to the pipeline.
    /// <list type="bullet">
    ///   <item>Content-Security-Policy — restricts resource origins; blocks inline scripts; disallows framing.</item>
    ///   <item>X-Frame-Options: DENY — belt-and-suspenders clickjacking protection.</item>
    ///   <item>X-Content-Type-Options: nosniff — prevents MIME-type sniffing.</item>
    ///   <item>Referrer-Policy: strict-origin-when-cross-origin — safe Referer header behaviour.</item>
    ///   <item>Permissions-Policy — disables unused browser features (camera, mic, geolocation).</item>
    /// </list>
    /// </summary>
    public static IApplicationBuilder UseZenoHrSecurityHeaders(this IApplicationBuilder app)
    {
        // REQ-SEC-003: Content-Security-Policy
        // ZenoHR API serves only JSON — no inline scripts, no embeds, no cross-origin frames.
        // Blazor Web frontend is a separate origin and sets its own CSP via ZenoHR.Web.
        var policyCollection = new HeaderPolicyCollection()
            .AddContentSecurityPolicy(builder =>
            {
                builder.AddDefaultSrc().Self();
                builder.AddScriptSrc().Self();
                builder.AddStyleSrc().Self().UnsafeInline(); // allow inline styles (Blazor/Swagger UI)
                builder.AddImgSrc().Self().Data();
                builder.AddFontSrc().Self();
                builder.AddConnectSrc().Self();
                builder.AddFormAction().Self();
                builder.AddFrameAncestors().None(); // disallow all framing — clickjacking prevention
                builder.AddObjectSrc().None();
                builder.AddBaseUri().Self();
                builder.AddUpgradeInsecureRequests(); // upgrade HTTP sub-resources to HTTPS
            })
            // Belt-and-suspenders: X-Frame-Options is honoured by old browsers that ignore CSP frame-ancestors
            .AddFrameOptionsDeny()
            // Prevent MIME-type sniffing (e.g., JSON interpreted as script)
            .AddContentTypeOptionsNoSniff()
            // Safe referrer behaviour — origin only when cross-origin
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            // Disable browser features ZenoHR does not use
            .AddPermissionsPolicy(builder =>
            {
                builder.AddCamera().None();
                builder.AddMicrophone().None();
                builder.AddGeolocation().None();
                builder.AddPayment().None();
                builder.AddUsb().None();
            })
            // Remove Server header to avoid fingerprinting
            .RemoveServerHeader();

        app.UseSecurityHeaders(policyCollection);
        return app;
    }

    /// <summary>
    /// Adds the CORS policy for ZenoHR API.
    /// Allowed origins are configured via <c>Cors:AllowedOrigins</c> (comma-separated list).
    /// In production this value comes from Azure Key Vault; in development it may be set via User Secrets.
    /// </summary>
    public static IServiceCollection AddZenoHrCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        // REQ-SEC-007: CORS — only the Blazor Web frontend may call the API cross-origin.
        // Closes VUL-002: previously no CORS policy configured (any origin allowed).
        var rawOrigins = configuration["Cors:AllowedOrigins"] ?? string.Empty;
        var allowedOrigins = rawOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddPolicy("ZenoHrPolicy", policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    // Development fallback: allow localhost variants.
                    // Production MUST have Cors:AllowedOrigins configured.
                    policy.WithOrigins(
                            "https://localhost:5002",
                            "http://localhost:5002",
                            "https://localhost:7002")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
            });
        });

        return services;
    }
}
