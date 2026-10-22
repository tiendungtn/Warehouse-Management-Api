using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuanLyKho.Models;

namespace QuanLyKho.Services;
public sealed class IssueService
{
    private readonly ApplicationDbContext _context;

    public IssueService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IssueListDto>> GetAllAsync()
    {
        return await _context.Issues
            .AsNoTracking()
            .Include(x => x.Creator)
            .OrderByDescending(x => x.IssueDate)
            .Select(x => new IssueListDto(
                x.Id,
                x.IssueCode,
                x.Reason,
                x.CreatedBy,
                x.Creator != null
                    ? x.Creator.Fullname
                    : string.Empty,
                x.IssueDate,
                x.Status,
                x.Invoice != null
            ))
            .ToListAsync();
    }

    public async Task<IssueDto> GetByIdAsync(int id)
    {
        var issue = await _context.Issues
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.Invoice)
            .Include(x => x.IssueDetails)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (issue == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy phiếu xuất.");
        }

        var items = issue.IssueDetails
            .Select(x => new IssueDetailDto(
                x.ProductId,
                x.Product?.ProductCode
                    ?? string.Empty,
                x.Product?.ProductName
                    ?? string.Empty,
                x.Product?.Unit
                    ?? string.Empty,
                x.Quantity,
                x.UnitPrice,
                x.Quantity *
                    (x.UnitPrice)
            ))
            .ToList();

        return new IssueDto(
            issue.Id,
            issue.IssueCode,
            issue.Reason,
            issue.CreatedBy,
            issue.Creator?.Fullname
                ?? string.Empty,
            issue.IssueDate,
            issue.Status,
            issue.Invoice != null,
            items,
            items.Sum(x => x.TotalAmount)
        );
    }

    public async Task<IssueDto> CreateAsync(
        IssueRequest request,
        int userId)
    {
        if (string.IsNullOrWhiteSpace(
                request.Reason))
        {
            throw new ArgumentException(
                "Vui lòng nhập lý do xuất kho.");
        }

        var validItems = request.Items
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(group => new IssueItemRequest
            {
                ProductId = group.Key,

                Quantity = group.Sum(
                    x => x.Quantity)
            })
            .ToList();

        if (validItems.Count == 0)
        {
            throw new InvalidOperationException(
                "Phiếu xuất phải có ít nhất " +
                "một mặt hàng.");
        }

        var productIds = validItems
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var products = await _context.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        foreach (var item in validItems)
        {
            if (!products.TryGetValue(
                    item.ProductId,
                    out var product))
            {
                throw new InvalidOperationException(
                    $"Sản phẩm ID {item.ProductId} " +
                    "không tồn tại.");
            }

            if (product.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Mặt hàng '{product.ProductName}' " +
                    $"chỉ còn {product.StockQuantity}, " +
                    "không đủ số lượng để xuất.");
            }
        }

        var issue = new Issue
        {
            IssueCode = await GenerateIssueCodeAsync(),
            Reason = request.Reason.Trim(),
            CreatedBy = userId,
            IssueDate = DateTime.UtcNow,
            Status = "Pending"
        };

        foreach (var item in validItems)
        {
            issue.IssueDetails.Add(
                new IssueDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
        }

        _context.Issues.Add(issue);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(issue.Id);
    }

    public async Task<InvoiceDto> ApproveAsync(
        int issueId,
        string customerName)
    {
        if (string.IsNullOrWhiteSpace(
                customerName))
        {
            throw new ArgumentException(
                "Vui lòng nhập tên khách hàng.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            var issue = await _context.Issues
                .Include(x => x.Creator)
                .Include(x => x.IssueDetails)
                    .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(
                    x => x.Id == issueId);

            if (issue == null)
            {
                throw new KeyNotFoundException(
                    "Không tìm thấy phiếu xuất.");
            }

            if (issue.Status != "Pending")
            {
                throw new InvalidOperationException(
                    "Phiếu xuất này đã được xử lý.");
            }

            decimal totalAmount = 0;

            foreach (var detail in issue.IssueDetails)
            {
                var product = detail.Product;

                if (product == null)
                {
                    throw new InvalidOperationException(
                        "Một sản phẩm trong phiếu " +
                        "xuất không tồn tại.");
                }

                if (product.StockQuantity <
                    detail.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Không đủ tồn kho cho " +
                        $"'{product.ProductName}'. " +
                        $"Tồn hiện tại: " +
                        $"{product.StockQuantity}.");
                }

                product.StockQuantity -=
                    detail.Quantity;

                totalAmount +=
                    detail.Quantity *
                    product.Price;

                detail.UnitPrice = product.Price;
            }

            issue.Status = "Approved";

            var invoice = new Invoice
            {
                InvoiceCode =
                    await GenerateInvoiceCodeAsync(),

                IssueId = issue.Id,

                CustomerName =
                    customerName.Trim(),

                TotalAmount =
                    totalAmount,

                CreatedDate =
                    DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            var invoiceItems =
                issue.IssueDetails
                    .Select(detail =>
                        new InvoiceItemDto(
                            detail.Product!.ProductCode,
                            detail.Product.ProductName,
                            detail.Product.Unit,
                            detail.Quantity,
                            detail.Product.Price,
                            detail.Quantity *
                                detail.Product.Price
                        ))
                    .ToList();

            return new InvoiceDto(
                invoice.Id,
                invoice.InvoiceCode,
                issue.Id,
                issue.IssueCode,
                invoice.CustomerName,
                issue.Creator?.Fullname
                    ?? string.Empty,
                invoice.TotalAmount,
                invoice.CreatedDate,
                invoiceItems
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string>
        GenerateIssueCodeAsync()
    {
        var prefix =
            $"PX-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var code = prefix;
        var suffix = 1;

        while (await _context.Issues
            .AnyAsync(x => x.IssueCode == code))
        {
            code = $"{prefix}-{suffix}";
            suffix++;
        }

        return code;
    }

    private async Task<string>
        GenerateInvoiceCodeAsync()
    {
        var prefix =
            $"HD-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var code = prefix;
        var suffix = 1;

        while (await _context.Invoices
            .AnyAsync(x => x.InvoiceCode == code))
        {
            code = $"{prefix}-{suffix}";
            suffix++;
        }

        return code;
    }

    public async Task<PagedResult<IssueListDto>> SearchAsync(
        IssueSearchRequest request)
    {
        var (page, pageSize) =
            PagingHelper.Normalize(request.Page, request.PageSize);

        if (request.FromDate.HasValue &&
            request.ToDate.HasValue &&
            request.FromDate.Value.Date > request.ToDate.Value.Date)
            throw new ArgumentException(
                "Ngày bắt đầu không được sau ngày kết thúc.");

        var query = _context.Issues
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.IssueCode.Contains(keyword) ||
                x.Reason.Contains(keyword) ||
                (x.Creator != null &&
                 x.Creator.Fullname.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.HasInvoice.HasValue)
        {
            query = request.HasInvoice.Value
                ? query.Where(x => x.Invoice != null)
                : query.Where(x => x.Invoice == null);
        }

        if (request.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(
                request.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.IssueDate >= from);
        }

        if (request.ToDate.HasValue)
        {
            var until = DateTime.SpecifyKind(
                request.ToDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);
            query = query.Where(x => x.IssueDate < until);
        }

        var descending = string.Equals(
            request.SortOrder, "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "date";

        var ordered = sortBy switch
        {
            "code" => descending
                ? query.OrderByDescending(x => x.IssueCode)
                : query.OrderBy(x => x.IssueCode),

            "status" => descending
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),

            "reason" => descending
                ? query.OrderByDescending(x => x.Reason)
                : query.OrderBy(x => x.Reason),

            "date" => descending
                ? query.OrderByDescending(x => x.IssueDate)
                : query.OrderBy(x => x.IssueDate),

            _ => throw new ArgumentException(
                "SortBy phải là date, code, status hoặc reason.")
        };

        var totalItems = await query.CountAsync();

        var items = await ordered
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new IssueListDto(
                x.Id,
                x.IssueCode,
                x.Reason,
                x.CreatedBy,
                x.Creator != null
                    ? x.Creator.Fullname
                    : string.Empty,
                x.IssueDate,
                x.Status,
                x.Invoice != null))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<IssueListDto>(
            items, page, pageSize, totalItems, totalPages);
    }

}
