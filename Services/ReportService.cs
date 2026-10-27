using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using System.Text;

namespace QuanLyKho.Services;
public sealed class ReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockReportDto>
        GetStockReportAsync(
            string? search,
            int? categoryId,
            string? stockFilter)
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

        if (categoryId.HasValue)
        {
            query = query.Where(
                x => x.CategoryId ==
                     categoryId.Value);
        }

        switch (
            stockFilter?
                .Trim()
                .ToLowerInvariant())
        {
            case "outofstock":

                query = query.Where(
                    x => x.StockQuantity == 0);

                break;

            case "lowstock":

                query = query.Where(
                    x =>
                        x.StockQuantity > 0 &&
                        x.StockQuantity <= 5);

                break;

            case "instock":

                query = query.Where(
                    x =>
                        x.StockQuantity > 5);

                break;
        }

        var products = await query
            .OrderBy(x => x.StockQuantity)
            .ThenBy(x => x.ProductName)
            .Select(x =>
                new StockReportItemDto(
                    x.Id,
                    x.ProductCode,
                    x.ProductName,
                    x.Category != null
                        ? x.Category.CategoryName
                        : string.Empty,
                    x.Unit,
                    x.Price,
                    x.StockQuantity,
                    x.StockQuantity *
                        x.Price,
                    x.StockQuantity == 0
                        ? "OutOfStock"
                        : x.StockQuantity <= 5
                            ? "LowStock"
                            : "InStock"
                ))
            .ToListAsync();

        return new StockReportDto(
            products.Count,

            products.Sum(
                x => x.StockQuantity),

            products.Sum(
                x => x.TotalValuation),

            products.Count(
                x => x.StockQuantity == 0),

            products.Count(
                x =>
                    x.StockQuantity > 0 &&
                    x.StockQuantity <= 5),

            products
        );
    }

    public async Task<RevenueReportDto>
        GetRevenueReportAsync(
            DateTime? fromDate,
            DateTime? toDate)
    {
        var query = _context.Invoices
            .AsNoTracking()
            .Include(x => x.Issue)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var fromUtc =
                DateTime.SpecifyKind(
                    fromDate.Value.Date,
                    DateTimeKind.Utc);

            query = query.Where(
                x => x.CreatedDate >=
                     fromUtc);
        }

        if (toDate.HasValue)
        {
            var endDate =
                toDate.Value.Date
                    .AddDays(1)
                    .AddTicks(-1);

            var endUtc =
                DateTime.SpecifyKind(
                    endDate,
                    DateTimeKind.Utc);

            query = query.Where(
                x => x.CreatedDate <=
                     endUtc);
        }

        var invoices =
            await query
                .OrderByDescending(
                    x => x.CreatedDate)
                .Select(x =>
                    new InvoiceListDto(
                        x.Id,
                        x.InvoiceCode,
                        x.IssueId,
                        x.Issue!.IssueCode,
                        x.CustomerName,
                        x.TotalAmount,
                        x.CreatedDate
                    ))
                .ToListAsync();

        return new RevenueReportDto(
            fromDate,
            toDate,
            invoices.Sum(
                x => x.TotalAmount),
            invoices
        );
    }

    public async Task<(
        byte[] Content,
        string FileName
    )> ExportStockCsvAsync()
    {
        var report =
            await GetStockReportAsync(
                null,
                null,
                null);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Mã hàng,Tên hàng,Danh mục," +
            "ĐVT,Đơn giá,Tồn kho," +
            "Tổng giá trị tồn");

        foreach (
            var item in report.Products)
        {
            builder.AppendLine(
                $"{Escape(item.ProductCode)}," +
                $"{Escape(item.ProductName)}," +
                $"{Escape(item.CategoryName)}," +
                $"{Escape(item.Unit)}," +
                $"{item.Price}," +
                $"{item.StockQuantity}," +
                $"{item.TotalValuation}"
            );
        }

        var preamble =
            Encoding.UTF8.GetPreamble();

        var content =
            Encoding.UTF8.GetBytes(
                builder.ToString());

        var result = new byte[
            preamble.Length +
            content.Length
        ];

        Buffer.BlockCopy(
            preamble,
            0,
            result,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            content,
            0,
            result,
            preamble.Length,
            content.Length);

        return (
            result,
            $"BaoCaoTonKho_" +
            $"{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
        );
    }

    private static string Escape(
        string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return "\"" +
            value.Replace("\"", "\"\"") +
            "\"";
    }

    public async Task<PagedResult<StockReportItemDto>> SearchStockAsync(
        StockSearchRequest request)
    {
        var (page, pageSize) =
            PagingHelper.Normalize(request.Page, request.PageSize);

        if (request.MinPrice < 0 || request.MaxPrice < 0)
            throw new ArgumentException("Đơn giá không được âm.");

        if (request.MinStock < 0 || request.MaxStock < 0)
            throw new ArgumentException("Tồn kho không được âm.");

        if (request.MinPrice.HasValue &&
            request.MaxPrice.HasValue &&
            request.MinPrice > request.MaxPrice)
            throw new ArgumentException(
                "Đơn giá tối thiểu không được lớn hơn tối đa.");

        if (request.MinStock.HasValue &&
            request.MaxStock.HasValue &&
            request.MinStock > request.MaxStock)
            throw new ArgumentException(
                "Tồn kho tối thiểu không được lớn hơn tối đa.");

        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.ProductCode.Contains(keyword) ||
                x.ProductName.Contains(keyword));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(
                x => x.CategoryId == request.CategoryId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(
                x => x.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(
                x => x.Price <= request.MaxPrice.Value);

        if (request.MinStock.HasValue)
            query = query.Where(
                x => x.StockQuantity >= request.MinStock.Value);

        if (request.MaxStock.HasValue)
            query = query.Where(
                x => x.StockQuantity <= request.MaxStock.Value);

        switch (request.StockFilter?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "all":
                break;

            case "outofstock":
                query = query.Where(x => x.StockQuantity == 0);
                break;

            case "lowstock":
                query = query.Where(
                    x => x.StockQuantity >= 1 &&
                         x.StockQuantity <= 5);
                break;

            case "instock":
                query = query.Where(x => x.StockQuantity > 5);
                break;

            default:
                throw new ArgumentException(
                    "StockFilter phải là all, outofstock, lowstock hoặc instock.");
        }

        var descending = string.Equals(
            request.SortOrder, "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "stock";


        var ordered = sortBy switch
        {
            "id" => descending
                ? query.OrderByDescending(x => x.Id)
                : query.OrderBy(x => x.Id),

            "code" => descending
                ? query.OrderByDescending(x => x.ProductCode)
                : query.OrderBy(x => x.ProductCode),

            "name" => descending
                ? query.OrderByDescending(x => x.ProductName)
                : query.OrderBy(x => x.ProductName),

            "price" => descending
                ? query.OrderByDescending(x => x.Price)
                : query.OrderBy(x => x.Price),

            "stock" => descending
                ? query.OrderByDescending(x => x.StockQuantity)
                : query.OrderBy(x => x.StockQuantity),

            "value" => descending
                ? query.OrderByDescending(x => x.StockQuantity * x.Price)
                : query.OrderBy(x => x.StockQuantity * x.Price),

            _ => throw new ArgumentException(
                "SortBy phải là id, code, name, price, stock hoặc value.")
        };


        var totalItems = await query.CountAsync();

        var items = await ordered
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StockReportItemDto(
                x.Id,
                x.ProductCode,
                x.ProductName,
                x.Category != null
                    ? x.Category.CategoryName
                    : string.Empty,
                x.Unit,
                x.Price,
                x.StockQuantity,
                x.StockQuantity * x.Price,
                x.StockQuantity == 0
                    ? "OutOfStock"
                    : x.StockQuantity <= 5
                        ? "LowStock"
                        : "InStock"))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<StockReportItemDto>(
            items, page, pageSize, totalItems, totalPages);
    }


    public async Task<StockReportDto> GetStockSummaryAsync(
        StockSearchRequest request)
    {
        // Tái sử dụng cùng bộ lọc và thống kê trên toàn bộ kết quả.
        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x =>
                x.ProductCode.Contains(keyword) ||
                x.ProductName.Contains(keyword));

        if (request.CategoryId.HasValue)
            query = query.Where(
                x => x.CategoryId == request.CategoryId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(x => x.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= request.MaxPrice.Value);

        if (request.MinStock.HasValue)
            query = query.Where(
                x => x.StockQuantity >= request.MinStock.Value);

        if (request.MaxStock.HasValue)
            query = query.Where(
                x => x.StockQuantity <= request.MaxStock.Value);

        switch (request.StockFilter?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "all":
                break;
            case "outofstock":
                query = query.Where(x => x.StockQuantity == 0);
                break;
            case "lowstock":
                query = query.Where(
                    x => x.StockQuantity >= 1 && x.StockQuantity <= 5);
                break;
            case "instock":
                query = query.Where(x => x.StockQuantity > 5);
                break;
            default:
                throw new ArgumentException("StockFilter không hợp lệ.");
        }

        var totalProducts = await query.CountAsync();

        var totalQuantity = await query
            .Select(x => (int?)x.StockQuantity)
            .SumAsync() ?? 0;

        var totalValuation = await query
            .Select(x => (decimal?)(x.StockQuantity * x.Price))
            .SumAsync() ?? 0m;

        var outOfStockCount = await query
            .CountAsync(x => x.StockQuantity == 0);

        var lowStockCount = await query
            .CountAsync(x =>
                x.StockQuantity >= 1 && x.StockQuantity <= 5);

        // Summary chỉ chứa các tổng số liệu; danh sách phân trang
        // được trả riêng từ SearchStockAsync.
        return new StockReportDto(
            totalProducts,
            totalQuantity,
            totalValuation,
            outOfStockCount,
            lowStockCount,
            Array.Empty<StockReportItemDto>());
    }

}
