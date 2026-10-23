using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.Models;

namespace QuanLyKho.Services;

public sealed class PermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(
        int userId,
        string permissionCode)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return false;

        var roleHasPermission = await _context.RolePermissions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Role == user.Role &&
                x.Permission.Code == permissionCode);

        if (user.PermissionMode != "Custom")
            return roleHasPermission;

        var userOverride = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Permission.Code == permissionCode)
            .Select(x => x.Effect)
            .FirstOrDefaultAsync();

        if (userOverride == "Deny")
            return false;

        if (userOverride == "Grant")
            return true;

        return roleHasPermission;
    }

    public async Task<IReadOnlyList<string>>
        GetEffectivePermissionsAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return Array.Empty<string>();

        var rolePermissions = await _context.RolePermissions
            .AsNoTracking()
            .Where(x => x.Role == user.Role)
            .Select(x => x.Permission.Code)
            .ToListAsync();

        if (user.PermissionMode != "Custom")
            return rolePermissions.Distinct().ToArray();

        var overrides = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.Permission.Code,
                x.Effect
            })
            .ToListAsync();

        var effective = new HashSet<string>(rolePermissions);

        foreach (var item in overrides)
        {
            if (item.Effect == "Deny")
                effective.Remove(item.Code);
            else if (item.Effect == "Grant")
                effective.Add(item.Code);
        }

        return effective.OrderBy(x => x).ToArray();
    }
}