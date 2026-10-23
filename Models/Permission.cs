using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.Models;

public sealed class Permission
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();

    public ICollection<UserPermissionOverride> UserOverrides { get; set; }
        = new List<UserPermissionOverride>();
}