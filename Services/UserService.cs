using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuanLyKho.Models;

namespace QuanLyKho.Services;
public sealed class UserService
{
    private static readonly string[] ValidRoles =
    {
        "Admin",
        "Manager",
        "Staff"
    };

    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Select(x => new UserDto(
                x.Id,
                x.Username,
                x.Fullname,
                x.Role,
                x.CreatedAt))
            .ToListAsync();
    }

    public async Task<UserDto> CreateAsync(
        CreateUserRequest request)
    {
        ValidateRole(request.Role);
        ValidateUserInput(request.Fullname, request.Password);

        var username = request.Username.Trim();

        if (await _context.Users.AnyAsync(
            x => x.Username == username))
        {
            throw new InvalidOperationException(
                "Tên đăng nhập đã được sử dụng.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var user = new User
        {
            Username = username,
            Password = BCrypt.Net.BCrypt.HashPassword(
                request.Password),
            Fullname = request.Fullname.Trim(),
            Role = NormalizeRole(request.Role),
            PermissionMode = request.PermissionMode,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // Lấy User.Id trước khi tạo các quyền ghi đè.
        await _context.SaveChangesAsync();

        await ReplacePermissionOverridesAsync(
            user,
            request.PermissionMode,
            request.PermissionOverrides,
            updatedBy: null);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(
        int id,
        UpdateUserRequest request,
        int updatedBy)
    {
        ValidateRole(request.Role);
        ValidateUserInput(request.Fullname);

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy người dùng.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        user.Fullname = request.Fullname.Trim();
        user.Role = NormalizeRole(request.Role);

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 6)
            {
                throw new ArgumentException(
                    "Mật khẩu mới phải có ít nhất 6 ký tự.");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(
                request.NewPassword);
        }

        await ReconcilePermissionOverridesAsync(user);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(user);
    }

    public async Task DeleteAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new InvalidOperationException(
                "Bạn không thể tự xóa tài khoản đang đăng nhập.");
        }

        var user = await _context.Users
            .Include(x => x.Receipts)
            .Include(x => x.Issues)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy người dùng.");

        if (user.Receipts.Count > 0 || user.Issues.Count > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xóa tài khoản '{user.Username}' " +
                "vì đã có lịch sử lập phiếu.");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    private async Task ReplacePermissionOverridesAsync(
        User user,
        string permissionMode,
        IReadOnlyCollection<UserPermissionOverrideRequest> requested,
        int? updatedBy)
    {
        if (permissionMode is not ("RoleBased" or "Custom"))
        {
            throw new ArgumentException(
                "PermissionMode chỉ nhận RoleBased hoặc Custom.");
        }

        if (requested is null)
        {
            throw new ArgumentException(
                "Danh sách PermissionOverrides không được null.");
        }

        if (requested.Count > 2)
        {
            throw new InvalidOperationException(
                "Mỗi người dùng chỉ được ghi đè tối đa 2 quyền.");
        }

        if (permissionMode == "RoleBased" && requested.Count > 0)
        {
            throw new InvalidOperationException(
                "RoleBased không được cấu hình quyền ghi đè cá nhân.");
        }

        var normalized = requested.Select(x =>
        {
            if (x is null ||
                string.IsNullOrWhiteSpace(x.PermissionCode) ||
                string.IsNullOrWhiteSpace(x.Effect))
            {
                throw new ArgumentException(
                    "PermissionCode và Effect là bắt buộc.");
            }

            return new
            {
                Code = x.PermissionCode.Trim(),
                Effect = x.Effect.Trim(),
                Reason = string.IsNullOrWhiteSpace(x.Reason)
                    ? null
                    : x.Reason.Trim()
            };
        }).ToList();

        if (normalized.GroupBy(
                x => x.Code,
                StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException(
                "Không được khai báo trùng một quyền.");
        }

        if (normalized.Any(x =>
            (x.Effect != "Grant" && x.Effect != "Deny") ||
            (x.Reason?.Length ?? 0) > 300))
        {
            throw new ArgumentException(
                "Effect chỉ nhận Grant hoặc Deny; Reason tối đa 300 ký tự.");
        }

        var codes = normalized
            .Select(x => x.Code)
            .ToArray();

        var permissions = await _context.Permissions
            .Where(x => codes.Contains(x.Code))
            .ToListAsync();

        if (permissions.Count != codes.Length)
        {
            throw new InvalidOperationException(
                "Có mã quyền không tồn tại trong danh mục quyền.");
        }

        var permissionMap = permissions.ToDictionary(
            x => x.Code,
            StringComparer.OrdinalIgnoreCase);

        var permissionIds = permissions
            .Select(x => x.Id)
            .ToArray();

        var roleGrantedIds = await _context.RolePermissions
            .Where(x =>
                x.Role == user.Role &&
                permissionIds.Contains(x.PermissionId))
            .Select(x => x.PermissionId)
            .ToHashSetAsync();

        foreach (var item in normalized)
        {
            var roleAllows = roleGrantedIds.Contains(
                permissionMap[item.Code].Id);

            if ((item.Effect == "Grant" && roleAllows) ||
                (item.Effect == "Deny" && !roleAllows))
            {
                throw new InvalidOperationException(
                    $"Quyền '{item.Code}' trùng với quyền cơ sở " +
                    "của vai trò '{user.Role}'; không cần ghi đè.");
            }
        }

        var oldOverrides = await _context.UserPermissionOverrides
            .Where(x => x.UserId == user.Id)
            .ToListAsync();

        _context.UserPermissionOverrides.RemoveRange(oldOverrides);

        user.PermissionMode = permissionMode;

        if (permissionMode == "Custom")
        {
            foreach (var item in normalized)
            {
                _context.UserPermissionOverrides.Add(
                    new UserPermissionOverride
                    {
                        UserId = user.Id,
                        PermissionId = permissionMap[item.Code].Id,
                        Effect = item.Effect,
                        Reason = item.Reason,
                        UpdatedBy = updatedBy,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
        }
    }

    private static void ValidateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) ||
            !ValidRoles.Contains(
                role.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Role phải là Admin, Manager hoặc Staff.");
        }
    }

    private static string NormalizeRole(string role)
    {
        return ValidRoles.First(
            x => string.Equals(
                x, role.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateUserInput(
        string fullname,
        string? password = null)
    {
        if (string.IsNullOrWhiteSpace(fullname))
        {
            throw new ArgumentException(
                "Họ tên không được để trống.");
        }

        if (password is not null && password.Length < 6)
        {
            throw new ArgumentException(
                "Mật khẩu phải có ít nhất 6 ký tự.");
        }
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.Fullname,
            user.Role,
            user.CreatedAt);
    }

    private async Task ReconcilePermissionOverridesAsync(User user)
    {
        var overrides = await _context.UserPermissionOverrides
            .Include(x => x.Permission)
            .Where(x => x.UserId == user.Id)
            .ToListAsync();

        if (overrides.Count == 0)
            return;

        var permissionIds = overrides
            .Select(x => x.PermissionId)
            .ToArray();

        var grantedIds = await _context.RolePermissions
            .Where(x =>
                x.Role == user.Role &&
                permissionIds.Contains(x.PermissionId))
            .Select(x => x.PermissionId)
            .ToHashSetAsync();

        var redundant = overrides.Where(item =>
            (item.Effect == "Grant" &&
             grantedIds.Contains(item.PermissionId)) ||
            (item.Effect == "Deny" &&
             !grantedIds.Contains(item.PermissionId))
        ).ToList();

        if (redundant.Count > 0)
            _context.UserPermissionOverrides.RemoveRange(redundant);
    }
}
