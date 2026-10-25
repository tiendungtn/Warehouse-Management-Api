using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
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
    public async Task SetOverridesAsync(
    int userId,
    string permissionMode,
    IReadOnlyCollection<UserPermissionOverrideRequest> requested,
    int updatedBy,
    CancellationToken cancellationToken = default)
    {
        if (permissionMode is not ("RoleBased" or "Custom"))
            throw new ArgumentException(
                "PermissionMode chỉ nhận RoleBased hoặc Custom.");

        if (requested.Count > 2)
            throw new InvalidOperationException(
                "Mỗi người dùng chỉ được ghi đè tối đa 2 quyền.");
        if (permissionMode == "RoleBased" && requested.Count > 0)
        {
            throw new InvalidOperationException(
                "RoleBased không được cấu hình quyền ghi đè cá nhân.");
        }

        var normalized = requested
            .Select(x => new
            {
                Code = x.PermissionCode.Trim(),
                Effect = x.Effect.Trim(),
                Reason = string.IsNullOrWhiteSpace(x.Reason)
                    ? null
                    : x.Reason.Trim()
            })
            .ToList();

        if (normalized.GroupBy(
                x => x.Code,
                StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException(
                "Không được khai báo trùng một quyền.");
        }

        if (normalized.Any(x =>
                x.Effect is not ("Grant" or "Deny")
                || x.Code.Length == 0
                || (x.Reason?.Length ?? 0) > 300))
        {
            throw new ArgumentException(
                "Mã quyền, Effect hoặc Reason không hợp lệ.");
        }

        var user = await _context.Users
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy người dùng.");

        var permissions = await _context.Permissions
            .Where(p => normalized.Select(x => x.Code).Contains(p.Code))
            .ToListAsync(cancellationToken);

        if (permissions.Count != normalized.Count)
            throw new InvalidOperationException(
                "Có mã quyền không tồn tại.");

        var permissionMap = permissions.ToDictionary(
            x => x.Code,
            StringComparer.OrdinalIgnoreCase);

        var permissionIds = permissions.Select(x => x.Id).ToArray();

        var roleGrantedIds = await _context.RolePermissions
            .Where(x => x.Role == user.Role
                        && permissionIds.Contains(x.PermissionId))
            .Select(x => x.PermissionId)
            .ToHashSetAsync(cancellationToken);

        foreach (var item in normalized)
        {
            var roleAllows = roleGrantedIds.Contains(
                permissionMap[item.Code].Id);

            if ((item.Effect == "Grant" && roleAllows)
                || (item.Effect == "Deny" && !roleAllows))
            {
                throw new InvalidOperationException(
                    $"'{item.Code}' đã trùng với quyền cơ sở của vai trò; " +
                    "không cần ghi đè.");
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        var oldOverrides = await _context.UserPermissionOverrides
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.UserPermissionOverrides.RemoveRange(oldOverrides);
        user.PermissionMode = permissionMode;

        if (permissionMode == "Custom")
        {
            foreach (var item in normalized)
            {
                _context.UserPermissionOverrides.Add(
                    new UserPermissionOverride
                    {
                        UserId = userId,
                        PermissionId = permissionMap[item.Code].Id,
                        Effect = item.Effect,
                        Reason = item.Reason,
                        UpdatedBy = updatedBy,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}