using Microsoft.AspNetCore.Authorization;

namespace QuanLyKho.Authorization;

public sealed record PermissionRequirement(string PermissionCode)
    : IAuthorizationRequirement;