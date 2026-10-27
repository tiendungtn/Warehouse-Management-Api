using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuanLyKho.Models;

namespace QuanLyKho.Services;

public sealed class CategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.CategoryName)
            .Select(x => new CategoryDto(
                x.Id,
                x.CategoryName,
                x.Description,
                x.Products.Count))
            .ToListAsync();
    }

    public async Task<PagedResult<CategoryDto>> SearchAsync(
        CategorySearchRequest request)
    {
        var (page, pageSize) = PagingHelper.Normalize(
            request.Page,
            request.PageSize);

        var query = _context.Categories
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.CategoryName.Contains(keyword) ||
                (x.Description != null &&
                 x.Description.Contains(keyword)));
        }

        if (request.HasProducts.HasValue)
        {
            query = request.HasProducts.Value
                ? query.Where(x => x.Products.Any())
                : query.Where(x => !x.Products.Any());
        }

        if (request.MinProductCount.HasValue)
        {
            query = query.Where(x =>
                x.Products.Count >= request.MinProductCount.Value);
        }

        if (request.MaxProductCount.HasValue)
        {
            query = query.Where(x =>
                x.Products.Count <= request.MaxProductCount.Value);
        }

        if (request.MinProductCount < 0 ||
            request.MaxProductCount < 0)
        {
            throw new ArgumentException(
                "Số lượng sản phẩm không được âm.");
        }

        if (request.MinProductCount.HasValue &&
            request.MaxProductCount.HasValue &&
            request.MinProductCount > request.MaxProductCount)
        {
            throw new ArgumentException(
                "Số lượng sản phẩm tối thiểu không được lớn hơn tối đa.");
        }

        var descending = string.Equals(
            request.SortOrder,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "name";

        query = sortBy switch
        {
            "id" => descending
                ? query.OrderByDescending(x => x.Id)
                : query.OrderBy(x => x.Id),

            "count" => descending
                ? query.OrderByDescending(x => x.Products.Count)
                : query.OrderBy(x => x.Products.Count),

            "name" => descending
                ? query.OrderByDescending(x => x.CategoryName)
                : query.OrderBy(x => x.CategoryName),

            _ => throw new ArgumentException(
                "SortBy phải là id, name hoặc count.")
        };


        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CategoryDto(
                x.Id,
                x.CategoryName,
                x.Description,
                x.Products.Count))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<CategoryDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<CategoryDto> GetByIdAsync(int id)
    {
        return await _context.Categories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CategoryDto(
                x.Id,
                x.CategoryName,
                x.Description,
                x.Products.Count))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException(
                "Không tìm thấy danh mục.");
    }

    public async Task<CategoryDto> CreateAsync(
        CategoryRequest request)
    {
        var name = request.CategoryName.Trim();

        if (await _context.Categories.AnyAsync(
                x => x.CategoryName == name))
        {
            throw new InvalidOperationException(
                "Danh mục đã tồn tại.");
        }

        var entity = new Category
        {
            CategoryName = name,
            Description =
                string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim()
        };

        _context.Categories.Add(entity);

        await _context.SaveChangesAsync();

        return new CategoryDto(
            entity.Id,
            entity.CategoryName,
            entity.Description,
            0);
    }

    public async Task<CategoryDto> UpdateAsync(
        int id,
        CategoryRequest request)
    {
        var entity = await _context.Categories
            .FindAsync(id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy danh mục.");

        var name = request.CategoryName.Trim();

        if (await _context.Categories.AnyAsync(
                x => x.Id != id &&
                     x.CategoryName == name))
        {
            throw new InvalidOperationException(
                "Danh mục đã tồn tại.");
        }

        entity.CategoryName = name;

        entity.Description =
            string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Categories
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy danh mục.");

        if (entity.Products.Count > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xóa danh mục {entity.CategoryName} " +
                "vì đang có sản phẩm liên kết.");
        }

        _context.Categories.Remove(entity);

        await _context.SaveChangesAsync();
    }
}