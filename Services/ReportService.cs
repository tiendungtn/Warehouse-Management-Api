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

    public async Task<MonthlyRevenueReportDto>
    GetMonthlyRevenueReportAsync(
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            throw new ArgumentException(
                "Ngày bắt đầu không được sau ngày kết thúc.");
        }

        // Tính khoảng ngày theo dạng [đầu ngày, đầu ngày kế tiếp).
        var query = _context.Invoices
            .AsNoTracking()
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var start = DateTime.SpecifyKind(
                fromDate.Value.Date,
                DateTimeKind.Utc);

            query = query.Where(x => x.CreatedDate >= start);
        }

        if (toDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(
                toDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);

            query = query.Where(x => x.CreatedDate < endExclusive);
        }

        // Lấy các hóa đơn và dòng hàng của phiếu xuất liên kết.
        // Không lọc theo trạng thái: tuân theo lựa chọn all-existing.
        var invoices = await query
            .Include(x => x.Issue)
                .ThenInclude(x => x!.IssueDetails)
                    .ThenInclude(x => x.Product)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var invoiceDetails = invoices.Select(invoice =>
            new RevenueInvoiceDetailDto(
                invoice.Id,
                invoice.InvoiceCode,
                invoice.Issue?.IssueCode ?? string.Empty,
                invoice.CustomerName,
                invoice.CreatedDate,
                invoice.TotalAmount,
                (invoice.Issue?.IssueDetails
                    ?? new List<QuanLyKho.Models.IssueDetail>())
                    .Select(detail => new RevenueProductDto(
                        detail.ProductId,
                        detail.Product?.ProductCode ?? string.Empty,
                        detail.Product?.ProductName ?? string.Empty,
                        detail.Product?.Unit ?? string.Empty,
                        detail.Quantity,
                        detail.UnitPrice,
                        detail.Quantity * detail.UnitPrice
                    ))
                    .ToList()
            )).ToList();

        var months = invoiceDetails
            .GroupBy(x => new
            {
                x.CreatedDate.Year,
                x.CreatedDate.Month
            })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .Select(group =>
            {
                var monthInvoices = group
                    .OrderByDescending(x => x.CreatedDate)
                    .ToList();

                return new RevenueMonthDto(
                    group.Key.Year,
                    group.Key.Month,
                    $"Tháng {group.Key.Month:D2}/{group.Key.Year}",
                    monthInvoices.Sum(x => x.TotalAmount),
                    monthInvoices.Count,
                    monthInvoices.Count == 0
                        ? 0m
                        : monthInvoices.Sum(x => x.TotalAmount)
                            / monthInvoices.Count,
                    null,
                    monthInvoices
                );
            })
            .ToList();

        // Tính mức thay đổi so với tháng liền trước trong chuỗi tháng
        // có dữ liệu. Nếu tháng trước bằng 0 thì không tính phần trăm.
        for (var i = 0; i < months.Count; i++)
        {
            if (i == 0)
                continue;

            var previous = months[i - 1].TotalRevenue;

            var change = previous == 0
                ? (decimal?)null
                : (months[i].TotalRevenue - previous)
                    / previous * 100m;

            months[i] = months[i] with
            {
                ChangePercent = change
            };
        }

        var totalRevenue = invoices.Sum(x => x.TotalAmount);

        return new MonthlyRevenueReportDto(
            fromDate,
            toDate,
            totalRevenue,
            invoices.Count,
            invoices.Count == 0
                ? 0m
                : totalRevenue / invoices.Count,
            months
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

    public async Task<(byte[] Content, string FileName)>
    ExportRevenueCsvAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string type)
    {
        var report = await GetMonthlyRevenueReportAsync(
            fromDate,
            toDate);

        var builder = new StringBuilder();

        if (string.Equals(
            type, "monthly",
            StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                "Nam,Thang,Doanh thu,So hoa don," +
                "Gia tri trung binh,Thay doi phan tram");

            foreach (var month in report.Months)
            {
                builder.AppendLine(string.Join(",",
                    month.Year,
                    month.Month,
                    month.TotalRevenue,
                    month.InvoiceCount,
                    month.AverageInvoiceValue,
                    month.ChangePercent?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                        ?? ""));
            }
        }
        else if (string.Equals(
            type, "detail",
            StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                "Nam,Thang,Ma hoa don,Ma phieu xuat," +
                "Khach hang,Ngay lap,Ma san pham,Ten san pham," +
                "Don vi,So luong,Don gia,Thanh tien," +
                "Tong tien hoa don");

            foreach (var month in report.Months)
            {
                foreach (var invoice in month.Invoices)
                {
                    if (invoice.Items.Count == 0)
                    {
                        builder.AppendLine(string.Join(",",
                            month.Year,
                            month.Month,
                            Csv(invoice.InvoiceCode),
                            Csv(invoice.IssueCode),
                            Csv(invoice.CustomerName),
                            invoice.CreatedDate.ToString("yyyy-MM-dd"),
                            "", "", "", "", "", "",
                            invoice.TotalAmount));
                        continue;
                    }

                    foreach (var item in invoice.Items)
                    {
                        builder.AppendLine(string.Join(",",
                            month.Year,
                            month.Month,
                            Csv(invoice.InvoiceCode),
                            Csv(invoice.IssueCode),
                            Csv(invoice.CustomerName),
                            invoice.CreatedDate.ToString("yyyy-MM-dd"),
                            Csv(item.ProductCode),
                            Csv(item.ProductName),
                            Csv(item.Unit),
                            item.Quantity,
                            item.UnitPrice,
                            item.LineTotal,
                            invoice.TotalAmount));
                    }
                }
            }
        }
        else
        {
            throw new ArgumentException(
                "Loại xuất phải là monthly hoặc detail.");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var content = new byte[preamble.Length + body.Length];

        Buffer.BlockCopy(
            preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(
            body, 0, content, preamble.Length, body.Length);

        return (
            content,
            $"BaoCaoDoanhThu_{type}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
        );
    }

    private static string Csv(string? value)
    {
        var escaped = (value ?? "").Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

}
