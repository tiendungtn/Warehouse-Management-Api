namespace QuanLyKho.DTOs;

public sealed record UserPermissionOverrideDto(
    string PermissionCode,
    string Effect,
    string? Reason);

public sealed record UserPermissionConfigurationDto(
    string PermissionMode,
    IReadOnlyList<string> RolePermissions,
    IReadOnlyList<UserPermissionOverrideDto> PermissionOverrides,
    IReadOnlyList<string> EffectivePermissions);