using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.DTOs;

public sealed class UpdateUserPermissionsRequest
{
    [Required]
    [RegularExpression("^(RoleBased|Custom)$")]
    public string PermissionMode { get; set; } = "RoleBased";

    public List<UserPermissionOverrideRequest> PermissionOverrides
    { get; set; } = new();
}