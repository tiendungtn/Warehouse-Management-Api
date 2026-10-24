using Microsoft.EntityFrameworkCore;
using QuanLyKho.Models;

namespace QuanLyKho.Data;

public static class PermissionSeeder
{
    private sealed record PermissionDefinition(
        string Code,
        string Name,
        string Module,
        string? Description = null);

    private static readonly PermissionDefinition[] Catalog =
    [
        new("products.view", "Xem sản phẩm", "Products"),
        new("products.create", "Tạo sản phẩm", "Products"),
        new("products.edit", "Sửa sản phẩm", "Products"),
        new("products.delete", "Xóa sản phẩm", "Products"),

        new("categories.view", "Xem danh mục", "Categories"),
        new("categories.create", "Tạo danh mục", "Categories"),
        new("categories.edit", "Sửa danh mục", "Categories"),
        new("categories.delete", "Xóa danh mục", "Categories"),

        new("receipts.view", "Xem phiếu nhập", "Receipts"),
        new("receipts.create", "Tạo phiếu nhập", "Receipts"),
        new("receipts.edit", "Sửa phiếu nhập", "Receipts"),
        new("receipts.delete", "Xóa phiếu nhập", "Receipts"),
        new("receipts.approve", "Duyệt phiếu nhập", "Receipts"),

        new("issues.view", "Xem phiếu xuất", "Issues"),
        new("issues.create", "Tạo phiếu xuất", "Issues"),
        new("issues.edit", "Sửa phiếu xuất", "Issues"),
        new("issues.delete", "Xóa phiếu xuất", "Issues"),
        new("issues.approve", "Duyệt phiếu xuất", "Issues"),

        new("invoices.view", "Xem hóa đơn", "Invoices"),
        new("invoices.create", "Tạo hóa đơn", "Invoices"),
        new("invoices.edit", "Sửa hóa đơn", "Invoices"),
        new("invoices.delete", "Xóa hóa đơn", "Invoices"),
        new("invoices.download", "Tải hóa đơn", "Invoices"),

        new("reports.stock.view", "Xem báo cáo tồn kho", "Reports"),
        new("reports.stock.export", "Xuất báo cáo tồn kho", "Reports"),
        new("reports.revenue.view", "Xem báo cáo doanh thu", "Reports"),

        new("users.view", "Xem người dùng", "Users"),
        new("users.create", "Tạo người dùng", "Users"),
        new("users.edit", "Sửa người dùng", "Users"),
        new("users.lock", "Khóa người dùng", "Users"),
        new("users.permissions.manage", "Quản lý quyền người dùng", "Users"),

        new("roles.view", "Xem vai trò", "Roles"),
        new("roles.create", "Tạo vai trò", "Roles"),
        new("roles.edit", "Sửa vai trò", "Roles"),
        new("roles.delete", "Xóa vai trò", "Roles"),

        new("audit.view", "Xem nhật ký kiểm toán", "Audit")
    ];

    // Đây là quyền cơ sở theo nghiệp vụ; cần được áp dụng thống nhất
    // cho cả kiểm tra backend và giao diện.
    private static readonly HashSet<string> ManagerPermissions =
    [
        "products.view",
        "products.create",
        "products.edit",
        "categories.view",
        "categories.create",
        "categories.edit",
        "receipts.view",
        "receipts.create",
        "receipts.edit",
        "receipts.approve",
        "issues.view",
        "issues.create",
        "issues.edit",
        "issues.approve",
        "invoices.view",
        "invoices.create",
        "invoices.download",
        "reports.stock.view",
        "reports.stock.export",
        "reports.revenue.view"
    ];

    private static readonly HashSet<string> StaffPermissions =
    [
        "products.view",
        "categories.view",
        "receipts.view",
        "receipts.create",
        "issues.view",
        "issues.create",
        "invoices.view",
        "reports.stock.view"
    ];

    public static async Task SeedAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        // Thêm quyền còn thiếu, không xóa hoặc ghi đè dữ liệu đã có.
        var existingPermissions = await db.Permissions
            .ToListAsync(cancellationToken);

        foreach (var definition in Catalog)
        {
            var permission = existingPermissions
                .FirstOrDefault(x => x.Code == definition.Code);

            if (permission is null)
            {
                db.Permissions.Add(new Permission
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Module = definition.Module,
                    Description = definition.Description
                });
            }
            else
            {
                permission.Name = definition.Name;
                permission.Module = definition.Module;
                permission.Description = definition.Description;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var permissions = await db.Permissions
            .ToDictionaryAsync(
                x => x.Code,
                cancellationToken);

        var desired = new Dictionary<string, HashSet<string>>
        {
            ["Admin"] = Catalog.Select(x => x.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),

            ["Manager"] = ManagerPermissions,

            ["Staff"] = StaffPermissions
        };

        var existingRolePermissions = await db.RolePermissions
            .ToListAsync(cancellationToken);

        foreach (var (role, codes) in desired)
        {
            foreach (var code in codes)
            {
                var permissionId = permissions[code].Id;

                var exists = existingRolePermissions.Any(x =>
                    x.Role == role && x.PermissionId == permissionId);

                if (!exists)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        Role = role,
                        PermissionId = permissionId
                    });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}