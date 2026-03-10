// TC-SEC-022: Verify MFA enforcement on privileged operations.
// VUL-003 remediation test: RequireMfaAttribute must block requests without session_mfa=true.
// REQ-SEC-001, REQ-SEC-004, CTL-SEC-003

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using ZenoHR.Api.Auth;

namespace ZenoHR.Domain.Tests.Security;

/// <summary>
/// Unit tests for <see cref="RequireMfaAttribute"/>.
/// Verifies that privileged API operations are blocked when the Firebase JWT
/// does not include the <c>session_mfa=true</c> custom claim.
/// TC-SEC-022
/// </summary>
public sealed class RequireMfaAttributeTests
{
    private readonly RequireMfaAttribute _sut = new();

    // ─── TC-SEC-022-001: Authenticated user WITH session_mfa=true → allowed ──

    [Fact]
    public void OnAuthorization_WithMfaClaim_True_Allows()
    {
        // Arrange — authenticated user with session_mfa=true claim
        var context = BuildContext(
            isAuthenticated: true,
            claims: [new Claim("session_mfa", "true")]);

        // Act
        _sut.OnAuthorization(context);

        // Assert — no result set means the filter allowed the request
        context.Result.Should().BeNull(
            because: "an authenticated user with session_mfa=true must be allowed through");
    }

    // ─── TC-SEC-022-002: Authenticated user WITH session_mfa=false → 403 ─────

    [Fact]
    public void OnAuthorization_WithMfaClaim_False_Returns403()
    {
        // Arrange — authenticated user who passed login but skipped MFA
        var context = BuildContext(
            isAuthenticated: true,
            claims: [new Claim("session_mfa", "false")]);

        // Act
        _sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden,
                because: "an authenticated user with session_mfa=false has not completed MFA");

        var problem = ((ObjectResult)context.Result!).Value as ProblemDetails;
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("MFA Required");
    }

    // ─── TC-SEC-022-003: Authenticated user WITHOUT session_mfa claim → 403 ──

    [Fact]
    public void OnAuthorization_WithoutMfaClaim_Returns403()
    {
        // Arrange — authenticated user with no MFA claim at all (old token, SSO flow, etc.)
        var context = BuildContext(
            isAuthenticated: true,
            claims: [new Claim(ClaimTypes.NameIdentifier, "uid_test_001")]);

        // Act
        _sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden,
                because: "absence of the MFA claim must also be blocked — cannot assume MFA was performed");

        var problem = ((ObjectResult)context.Result!).Value as ProblemDetails;
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ─── TC-SEC-022-004: Unauthenticated request → 401 ──────────────────────

    [Fact]
    public void OnAuthorization_Unauthenticated_Returns401()
    {
        // Arrange — no authenticated identity (anonymous request)
        var context = BuildContext(
            isAuthenticated: false,
            claims: []);

        // Act
        _sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>(
            because: "an unauthenticated caller must receive 401 (not 403)");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="AuthorizationFilterContext"/> with the given identity and claims.
    /// </summary>
    private static AuthorizationFilterContext BuildContext(bool isAuthenticated, IEnumerable<Claim> claims)
    {
        ClaimsPrincipal principal;

        if (isAuthenticated)
        {
            var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
            principal = new ClaimsPrincipal(identity);
        }
        else
        {
            // Unauthenticated: ClaimsIdentity with no authentication type
            principal = new ClaimsPrincipal(new ClaimsIdentity());
        }

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(
            actionContext,
            filters: new List<IFilterMetadata>());
    }
}
