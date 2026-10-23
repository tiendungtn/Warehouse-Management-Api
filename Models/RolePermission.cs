namespace QuanLyKho.Models;

public sealed class RolePermission
{
    public int Id { get; set; }

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;

    // Giữ nguyên tên vai trò hiện tại: Admin, Manager, Staff.
    public string Role { get; set; } = string.Empty;
}