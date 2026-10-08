using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace LMS.Security;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode) =>
        Policy = PermissionCodes.PolicyPrefix + permissionCode;
}

public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionCodes.PolicyPrefix, StringComparison.Ordinal))
            return fallback.GetPolicyAsync(policyName);

        var code = policyName[PermissionCodes.PolicyPrefix.Length..];
        if (string.IsNullOrWhiteSpace(code))
            return Task.FromResult<AuthorizationPolicy?>(null);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(code))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var isAdmin = context.User.IsInRole(Roles.Admin);
        if (isAdmin || context.User.HasClaim(PermissionCodes.ClaimType, requirement.Code))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
