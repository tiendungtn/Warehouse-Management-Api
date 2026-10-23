using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.DTOs;

public sealed record UserDto(
    int Id,
    string Username,
    string Fullname,
    string Role,
    DateTime CreatedAt);

public sealed class CreateUserRequest
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = "";

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Fullname { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Staff";

    [Required]
    [RegularExpression("^(RoleBased|Custom)$")]
    public string PermissionMode { get; set; } = "RoleBased";

    public List<UserPermissionOverrideRequest> PermissionOverrides
    { get; set; } = new();
}

public sealed class UpdateUserRequest
{
    [Required]
    [MaxLength(200)]
    public string Fullname { get; set; } = "";

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Staff";

    public string? NewPassword { get; set; }

    [Required]
    [RegularExpression("^(RoleBased|Custom)$")]
    public string PermissionMode { get; set; } = "RoleBased";

    public List<UserPermissionOverrideRequest> PermissionOverrides
    { get; set; } = new();
}

public sealed class UserPermissionOverrideRequest
{
    [Required]
    [MaxLength(100)]
    public string PermissionCode { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Grant|Deny)$")]
    public string Effect { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Reason { get; set; }
}