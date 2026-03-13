// REQ-OPS-001: ZenoHR Blazor Server host — configures authentication, Razor Components, and middleware.
// REQ-SEC-001: Firebase JWT validation via Cookie auth + token exchange endpoint.
// REQ-SEC-002: Authorization policies enforce RBAC screen access matrix (PRD-15 Section 4).
// REQ-SEC-003: Authenticated route guards — unauthorized roles redirected to /unauthorized.
// TC-SEC-001: Route guard tests confirm that unauthorized roles cannot reach protected pages.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using ZenoHR.Infrastructure.Auth;
using ZenoHR.Infrastructure.Extensions;
using ZenoHR.Web.Components;
using ZenoHR.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Firebase project ID (required) ───────────────────────────────────────────
var firebaseProjectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException(
        "Firebase:ProjectId is required. " +
        "Set it via .NET User Secrets: dotnet user-secrets set Firebase:ProjectId <your-project-id>");

// ── Authentication ────────────────────────────────────────────────────────────
// REQ-SEC-001: Blazor Server uses Cookie auth (stateful SignalR session).
// Login flow: Firebase JS SDK -> client gets Firebase JWT -> POST /auth/exchange ->
// server validates JWT (Bearer "Firebase" scheme) -> issues ASP.NET Core cookie ->
// Blazor Server reads cookie for the lifetime of the session.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/unauthorized";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "zenohr.session";
    })
    .AddJwtBearer("Firebase", options =>
    {
        // Used only on POST /auth/exchange to validate the inbound Firebase JWT.
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true,
        };
        options.SaveToken = true;
    });

// ── ZenoHR RBAC claims transformation ────────────────────────────────────────
// Reads active user_role_assignments from Firestore to enrich the ClaimsPrincipal
// with ClaimTypes.Role. Falls back to Firestore query when Firebase custom claims
// are absent (dev / first login).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<UserRoleAssignmentRepository>();
builder.Services.AddSingleton<IClaimsTransformation, ZenoHR.Web.Auth.WebClaimsTransformation>();

// ── Authorization policies (PRD-15 Section 4) ────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ZenoHR.IsHR", policy =>
        policy.RequireRole("Director", "HRManager"));

    options.AddPolicy("ZenoHR.IsManager", policy =>
        policy.RequireRole("Director", "HRManager", "Manager"));

    options.AddPolicy("ZenoHR.IsEmployee", policy =>
        policy.RequireRole("Director", "HRManager", "Manager", "Employee"));

    options.AddPolicy("ZenoHR.IsSaasAdmin", policy =>
        policy.RequireRole("SaasAdmin"));

    // MFA-required: privileged mutations (REQ-SEC-004)
    options.AddPolicy("ZenoHR.RequiresMfa", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Director", "HRManager")
              .RequireAssertion(ctx =>
              {
                  var secondFactor = ctx.User.FindFirstValue("firebase.sign_in_second_factor");
                  return secondFactor is "phone" or "totp";
              }));
});

// ── Firestore + Infrastructure services ──────────────────────────────────────
builder.Services.AddZenoHrFirestore(builder.Configuration);

// ── Theme Service (REQ-OPS-008) ───────────────────────────────────────────────
// Scoped: each Blazor Server circuit gets its own ThemeService instance.
builder.Services.AddScoped<ThemeService>();

// ── Tour Service (REQ-OPS-001) ────────────────────────────────────────────────
// Scoped: manages product tour state and role-specific onboarding steps per circuit.
builder.Services.AddScoped<TourService>();

// ── Blazor Razor Components ───────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Pipeline ─────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// ── Security response headers (VUL-001) ─────────────────────────────────────
// REQ-SEC-003: CSP, X-Frame-Options, nosniff, Referrer-Policy for Blazor Server.
// Mirrors the API project's SecurityHeadersExtensions but tuned for Blazor Server
// (allows 'unsafe-inline' for scripts/styles, CDN fonts, and Lucide icons).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-XSS-Protection"] = "0";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Firebase token exchange endpoint ─────────────────────────────────────────
// REQ-SEC-001: Client POSTs Firebase ID token -> server validates -> issues cookie.
app.MapPost("/auth/exchange", async (HttpContext ctx) =>
{
    var authHeader = ctx.Request.Headers.Authorization.ToString();
    if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return Results.Unauthorized();

    var result = await ctx.AuthenticateAsync("Firebase");
    if (!result.Succeeded || result.Principal is null)
        return Results.Unauthorized();

    await ctx.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        result.Principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
        });

    return Results.Ok(new { message = "Authenticated" });
}).AllowAnonymous();

// ── Sign-out endpoint ─────────────────────────────────────────────────────────
app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).RequireAuthorization();

// ── Razor Components ─────────────────────────────────────────────────────────
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
