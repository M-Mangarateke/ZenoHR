// REQ-SEC-001, REQ-SEC-004, CTL-SEC-003: MFA enforcement on privileged operations.
// VUL-003 remediation: reject requests missing session_mfa=true Firebase custom claim.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ZenoHR.Api.Auth;

/// <summary>
/// Action filter attribute that blocks requests where the Firebase JWT does not
/// include the <c>session_mfa=true</c> custom claim.
/// <para>
/// Apply to: payroll finalize, compliance approve, and role management endpoints.
/// Any controller action or class decorated with this attribute will require
/// that the authenticated user has completed an MFA challenge in the current session.
/// </para>
/// REQ-SEC-001, REQ-SEC-004, CTL-SEC-003
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireMfaAttribute : Attribute, IAuthorizationFilter
{
    // VUL-003: Firebase custom claim set by the frontend after successful MFA challenge.
    // The claim name must match exactly what the Firebase Functions token-minting code sets.
    private const string MfaClaim = "session_mfa";

    /// <summary>
    /// Enforces the MFA precondition.
    /// Returns 401 for unauthenticated requests, 403 for authenticated users who have not completed MFA.
    /// REQ-SEC-004
    /// </summary>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // VUL-003: Check authentication first
        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // REQ-SEC-004: Privileged operations require MFA verification via custom claim
        var mfaClaim = user.FindFirst(MfaClaim);
        if (mfaClaim is null || !bool.TryParse(mfaClaim.Value, out var mfaVerified) || !mfaVerified)
        {
            // Return 403 Forbidden with ProblemDetails.
            // 403 (not 401) because the user IS authenticated — they just have not completed MFA.
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "MFA Required",
                Detail = "This operation requires multi-factor authentication. " +
                         "Please re-authenticate with MFA to continue.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
