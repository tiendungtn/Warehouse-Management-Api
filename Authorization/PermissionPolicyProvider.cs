using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace QuanLyKho.Authorization;

public sealed class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    private const string Prefix = "Permission:";

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(
        string policyName)
    {
        if (!policyName.StartsWith(
                Prefix,
                StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permissionCode = policyName[Prefix.Length..];

        if (string.IsNullOrWhiteSpace(permissionCode))
            return Task.FromResult<AuthorizationPolicy?>(null);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PermissionRequirement(permissionCode))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}