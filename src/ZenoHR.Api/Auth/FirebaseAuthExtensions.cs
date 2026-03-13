// REQ-SEC-001: Firebase JWT validation — authenticates requests using Firebase Auth OIDC tokens.
// REQ-SEC-002: Role-based authorization policies enforce screen access matrix (PRD-15 Section 4).
// REQ-SEC-003: All API endpoints require authentication (AllowAnonymous only on /health and /auth/login).
// REQ-SEC-004: MFA enforcement on privileged operations — closes VUL-003 (Sev-1).
// TC-SEC-001: JWT Bearer middleware validates issuer, audience, and token lifetime.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ZenoHR.Infrastructure.Auth;

namespace ZenoHR.Api.Auth;

/// <summary>
/// Named authorization policy constants matching the PRD-15 screen access matrix.
/// Use these with <c>[Authorize(Policy = ZenoHrPolicies.IsHR)]</c> on API endpoints.
/// For Blazor route components, use <c>[Authorize(Roles = "Director,HRManager")]</c> directly
/// (PRD-17 defines the per-page role constraints).
/// </summary>
public static class ZenoHrPolicies
{
    /// <summary>Director or HRManager — full tenant admin access (compliance, settings, audit).</summary>
    public const string IsHR = "ZenoHR.IsHR";

    /// <summary>Director, HRManager, or Manager — team-scoped data access (employees, leave, timesheets).</summary>
    public const string IsManager = "ZenoHR.IsManager";

    /// <summary>Any authenticated tenant user — Director, HRManager, Manager, or Employee.</summary>
    public const string IsEmployee = "ZenoHR.IsEmployee";

    /// <summary>SaasAdmin only — platform operations (/admin/* routes).</summary>
    public const string IsSaasAdmin = "ZenoHR.IsSaasAdmin";

    /// <summary>
    /// Privileged operations that require a verified MFA session.
    /// Firebase sets <c>firebase.sign_in_second_factor</c> claim when MFA was satisfied.
    /// Applies to: payroll finalize, role changes, compliance approvals, employee termination.
    /// REQ-SEC-004: closes VUL-003 — a stolen JWT alone cannot execute these operations.
    /// </summary>
    public const string RequiresMfa = "ZenoHR.RequiresMfa";
}

/// <summary>
/// Extension methods for configuring Firebase Authentication and ZenoHR RBAC on the ASP.NET Core pipeline.
/// </summary>
public static class FirebaseAuthExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication configured for Firebase Auth, the ZenoHR claims
    /// transformation middleware (<see cref="ZenoHrClaimsTransformation"/>), and the
    /// role-based authorization policies defined in <see cref="ZenoHrPolicies"/>.
    /// <para>
    /// Firebase issues JWTs signed by Google's RSA keys. The middleware discovers
    /// the signing keys automatically via the OIDC metadata endpoint at:
    /// <c>https://securetoken.google.com/{projectId}/.well-known/openid-configuration</c>
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="firebaseProjectId">Firebase project ID (e.g., "zenohr-prod").</param>
    public static IServiceCollection AddZenoHrFirebaseAuth(
        this IServiceCollection services, string firebaseProjectId)
    {
        // ── Firebase JWT validation ───────────────────────────────────────────
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Firebase OIDC discovery endpoint provides signing keys automatically.
                options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",

                    ValidateAudience = true,
                    ValidAudience = firebaseProjectId,

                    ValidateLifetime = true,
                };

                // Save the raw token so it can be forwarded to downstream services if needed.
                options.SaveToken = true;
            });

        // ── ZenoHR claims transformation ─────────────────────────────────────
        // Maps Firebase JWT claims (system_role, tenant_id, employee_id, dept_ids) to
        // ASP.NET Core ClaimTypes.Role so that [Authorize(Roles = "...")] works.
        // Falls back to Firestore query when custom claims are absent (dev/first-login).
        services.AddMemoryCache();
        services.AddSingleton<UserRoleAssignmentRepository>();
        services.AddSingleton<IClaimsTransformation, ZenoHrClaimsTransformation>();

        // ── Authorization policies (PRD-15 Section 4) ────────────────────────
        services.AddAuthorization(options =>
        {
            // HR-level: Director or HRManager
            // Used for: /compliance, /audit, /settings, /payroll management, /timesheets (full)
            options.AddPolicy(ZenoHrPolicies.IsHR, policy =>
                policy.RequireRole("Director", "HRManager"));

            // Manager-level: Director, HRManager, or Manager
            // Used for: /employees (team), /leave (team approve), /timesheets (team)
            options.AddPolicy(ZenoHrPolicies.IsManager, policy =>
                policy.RequireRole("Director", "HRManager", "Manager"));

            // Employee-level: any authenticated tenant user
            // Used for: /dashboard, /leave (own), /clock-in, /my-analytics, /profile, /payroll/my-payslips
            options.AddPolicy(ZenoHrPolicies.IsEmployee, policy =>
                policy.RequireRole("Director", "HRManager", "Manager", "Employee"));

            // SaasAdmin only: /admin/* routes
            options.AddPolicy(ZenoHrPolicies.IsSaasAdmin, policy =>
                policy.RequireRole("SaasAdmin"));

            // MFA-required: privileged mutations (payroll finalize, role changes, terminations)
            // REQ-SEC-004: Firebase sets "firebase.sign_in_second_factor" = "phone" (or "totp")
            // when the session included a second factor. A missing or non-"phone"/"totp" value
            // means the user authenticated with password only — reject privileged operations.
            // Closes VUL-003 (Sev-1): stolen JWT cannot finalize payroll or escalate privileges.
            options.AddPolicy(ZenoHrPolicies.RequiresMfa, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole("Director", "HRManager")
                      .RequireAssertion(ctx =>
                      {
                          // Firebase includes second-factor info in the "firebase" nested claim.
                          // The claim name after JWT parsing is "firebase.sign_in_second_factor".
                          var secondFactor = ctx.User.FindFirstValue("firebase.sign_in_second_factor");
                          return secondFactor is "phone" or "totp";
                      }));
        });

        return services;
    }
}
