using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.Models;

public sealed class UserPermissionOverride
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;

    // Chỉ nhận Grant hoặc Deny.
    [Required, MaxLength(10)]
    public string Effect { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Reason { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}