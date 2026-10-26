using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuanLyKho.Services;

namespace QuanLyKho.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly PermissionService _permissionService;

    public PermissionAuthorizationHandler(
        PermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
            return;

        var allowed = await _permissionService.HasPermissionAsync(
            userId,
            requirement.PermissionCode);

        if (allowed)
            context.Succeed(requirement);
    }
}