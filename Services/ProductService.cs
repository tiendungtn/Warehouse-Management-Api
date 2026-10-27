using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuanLyKho.Models;

namespace QuanLyKho.Services;

public sealed class ProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
        string? search)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(x =>
                x.ProductCode.Contains(keyword) ||
                x.ProductName.Contains(keyword));
        }

        return await query
            .OrderBy(x => x.ProductName)
            .Select(x => new ProductDto(
                x.Id,
                x.ProductCode,
                x.ProductName,
                x.CategoryId,
                x.Category!.CategoryName,
                x.Unit,
                x.Price,
                x.StockQuantity))
            .ToListAsync();
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(
        ProductSearchRequest request)
    {
        var (page, pageSize) = PagingHelper.Normalize(
            request.Page,
            request.PageSize);

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.ProductCode.Contains(keyword) ||
                x.ProductName.Contains(keyword) ||
                x.Unit.Contains(keyword));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x =>
                x.CategoryId == request.CategoryId.Value);
        }

        if (request.MinPrice.HasValue)
            query = query.Where(x => x.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= request.MaxPrice.Value);

        if (request.MinStock.HasValue)
            query = query.Where(x =>
                x.StockQuantity >= request.MinStock.Value);

        if (request.MaxStock.HasValue)
            query = query.Where(x =>
                x.StockQuantity <= request.MaxStock.Value);

        switch (request.StockFilter?.Trim().ToLowerInvariant())
        {
            case "outofstock":
                query = query.Where(x => x.StockQuantity == 0);
                break;

            case "lowstock":
                query = query.Where(x =>
                    x.StockQuantity >= 1 &&
                    x.StockQuantity <= 5);
                break;

            case "instock":
                query = query.Where(x => x.StockQuantity > 5);
                break;

            case null:
            case "":
            case "all":
                break;

            default:
                throw new ArgumentException(
                    "StockFilter không hợp lệ.");
        }

        if (request.MinPrice.HasValue &&
            request.MaxPrice.HasValue &&
            request.MinPrice > request.MaxPrice)
        {
            throw new ArgumentException(
                "Giá tối thiểu không được lớn hơn giá tối đa.");
        }

        if (request.MinStock.HasValue &&
            request.MaxStock.HasValue &&
            request.MinStock > request.MaxStock)
        {
            throw new ArgumentException(
                "Tồn kho tối thiểu không được lớn hơn tồn kho tối đa.");
        }

        var descending = string.Equals(
            request.SortOrder,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        query = (request.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "id" => descending
                ? query.OrderByDescending(x => x.Id)
                : query.OrderBy(x => x.Id),

            "code" => descending
                ? query.OrderByDescending(x => x.ProductCode)
                : query.OrderBy(x => x.ProductCode),

            "price" => descending
                ? query.OrderByDescending(x => x.Price)
                : query.OrderBy(x => x.Price),

            "stock" => descending
                ? query.OrderByDescending(x => x.StockQuantity)
                : query.OrderBy(x => x.StockQuantity),

            "name" or "" or null => descending
                ? query.OrderByDescending(x => x.ProductName)
                : query.OrderBy(x => x.ProductName),

            _ => throw new ArgumentException(
                "SortBy phải là id, name, code, price hoặc stock.")
        };


        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductDto(
                x.Id,
                x.ProductCode,
                x.ProductName,
                x.CategoryId,
                x.Category != null
                    ? x.Category.CategoryName
                    : string.Empty,
                x.Unit,
                x.Price,
                x.StockQuantity))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<ProductDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<ProductDto> GetByIdAsync(int id)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.Id == id)
            .Select(x => new ProductDto(
                x.Id,
                x.ProductCode,
                x.ProductName,
                x.CategoryId,
                x.Category!.CategoryName,
                x.Unit,
                x.Price,
                x.StockQuantity))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException(
                "Không tìm thấy sản phẩm.");
    }

    public async Task<ProductDto> CreateAsync(
        ProductRequest request)
    {
        var code =
            request.ProductCode
                .Trim()
                .ToUpperInvariant();

        if (await _context.Products.AnyAsync(
                x => x.ProductCode == code))
        {
            throw new InvalidOperationException(
                "Mã sản phẩm đã tồn tại.");
        }

        if (!await _context.Categories.AnyAsync(
                x => x.Id == request.CategoryId))
        {
            throw new InvalidOperationException(
                "Danh mục không tồn tại.");
        }

        var entity = new Product
        {
            ProductCode = code,
            ProductName = request.ProductName.Trim(),
            CategoryId = request.CategoryId,
            Unit = request.Unit.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        _context.Products.Add(entity);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<ProductDto> UpdateAsync(
        int id,
        ProductRequest request)
    {
        var entity = await _context.Products
            .FindAsync(id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy sản phẩm.");

        var code =
            request.ProductCode
                .Trim()
                .ToUpperInvariant();

        if (await _context.Products.AnyAsync(
                x => x.Id != id &&
                     x.ProductCode == code))
        {
            throw new InvalidOperationException(
                "Mã sản phẩm đã tồn tại.");
        }

        entity.ProductCode = code;
        entity.ProductName =
            request.ProductName.Trim();

        entity.CategoryId =
            request.CategoryId;

        entity.Unit =
            request.Unit.Trim();

        entity.Price =
            request.Price;

        entity.StockQuantity =
            request.StockQuantity;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Products
            .FindAsync(id)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy sản phẩm.");

        var hasReceiptHistory =
            await _context.ReceiptDetails
                .AnyAsync(x => x.ProductId == id);

        var hasIssueHistory =
            await _context.IssueDetails
                .AnyAsync(x => x.ProductId == id);

        if (hasReceiptHistory ||
            hasIssueHistory)
        {
            throw new InvalidOperationException(
                $"Không thể xóa {entity.ProductName} " +
                "vì sản phẩm đã có lịch sử nhập/xuất.");
        }

        _context.Products.Remove(entity);

        await _context.SaveChangesAsync();
    }
}