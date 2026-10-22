using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuanLyKho.Models;

namespace QuanLyKho.Services;
public sealed class ReceiptService
{
    private readonly ApplicationDbContext _context;

    public ReceiptService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ReceiptListDto>> GetAllAsync()
    {
        return await _context.Receipts
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.ReceiptDetails)
            .OrderByDescending(x => x.ReceiptDate)
            .Select(x => new ReceiptListDto(
                x.Id,
                x.ReceiptCode,
                x.SupplierName,
                x.CreatedBy,
                x.Creator != null
                    ? x.Creator.Fullname
                    : string.Empty,
                x.ReceiptDate,
                x.Status,
                x.ReceiptDetails.Sum(d =>
                    d.Quantity * d.ImportPrice)
            ))
            .ToListAsync();
    }

    public async Task<ReceiptDto> GetByIdAsync(int id)
    {
        var receipt = await _context.Receipts
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.ReceiptDetails)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (receipt == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy phiếu nhập.");
        }

        var items = receipt.ReceiptDetails
            .Select(x => new ReceiptDetailDto(
                x.ProductId,
                x.Product?.ProductCode ?? string.Empty,
                x.Product?.ProductName ?? string.Empty,
                x.Product?.Unit ?? string.Empty,
                x.Quantity,
                x.ImportPrice,
                x.Quantity * x.ImportPrice
            ))
            .ToList();

        return new ReceiptDto(
            receipt.Id,
            receipt.ReceiptCode,
            receipt.SupplierName,
            receipt.CreatedBy,
            receipt.Creator?.Fullname ?? string.Empty,
            receipt.ReceiptDate,
            receipt.Status,
            items.Sum(x => x.TotalAmount),
            items
        );
    }

    public async Task<ReceiptDto> CreateAsync(
        ReceiptRequest request,
        int userId)
    {
        if (string.IsNullOrWhiteSpace(
                request.SupplierName))
        {
            throw new ArgumentException(
                "Vui lòng nhập tên nhà cung cấp.");
        }

        var validItems = request.Items
            .Where(x => x.Quantity > 0)
            .ToList();

        if (validItems.Count == 0)
        {
            throw new InvalidOperationException(
                "Phiếu nhập phải có ít nhất một mặt hàng.");
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
            if (!products.ContainsKey(item.ProductId))
            {
                throw new InvalidOperationException(
                    $"Sản phẩm ID {item.ProductId} không tồn tại.");
            }
        }

        var receipt = new Receipt
        {
            ReceiptCode = await GenerateReceiptCodeAsync(),
            SupplierName = request.SupplierName.Trim(),
            CreatedBy = userId,
            ReceiptDate =
                request.ReceiptDate?.ToUniversalTime()
                ?? DateTime.UtcNow,
            Status = "Pending"
        };

        foreach (var item in validItems)
        {
            receipt.ReceiptDetails.Add(
                new ReceiptDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    ImportPrice = item.ImportPrice
                });
        }

        _context.Receipts.Add(receipt);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(receipt.Id);
    }

    public async Task ApproveAsync(int id)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var receipt = await _context.Receipts
                .Include(x => x.ReceiptDetails)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (receipt == null)
            {
                throw new KeyNotFoundException(
                    "Không tìm thấy phiếu nhập.");
            }

            if (receipt.Status != "Pending")
            {
                throw new InvalidOperationException(
                    "Phiếu nhập này đã được xử lý trước đó.");
            }

            foreach (var detail in receipt.ReceiptDetails)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(
                        x => x.Id == detail.ProductId);

                if (product == null)
                {
                    throw new InvalidOperationException(
                        $"Sản phẩm ID {detail.ProductId} " +
                        "không tồn tại.");
                }

                product.StockQuantity += detail.Quantity;
            }

            receipt.Status = "Approved";

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GenerateReceiptCodeAsync()
    {
        var prefix =
            $"PN-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var code = prefix;
        var suffix = 1;

        while (await _context.Receipts
            .AnyAsync(x => x.ReceiptCode == code))
        {
            code = $"{prefix}-{suffix}";
            suffix++;
        }

        return code;
    }

    public async Task<PagedResult<ReceiptListDto>> SearchAsync(
        ReceiptSearchRequest request)
    {
        var (page, pageSize) =
            PagingHelper.Normalize(request.Page, request.PageSize);

        if (request.FromDate.HasValue &&
            request.ToDate.HasValue &&
            request.FromDate.Value.Date > request.ToDate.Value.Date)
            throw new ArgumentException(
                "Ngày bắt đầu không được sau ngày kết thúc.");

        if (request.MinAmount < 0 || request.MaxAmount < 0)
            throw new ArgumentException("Tổng tiền không được âm.");

        if (request.MinAmount.HasValue &&
            request.MaxAmount.HasValue &&
            request.MinAmount > request.MaxAmount)
            throw new ArgumentException(
                "Tổng tiền tối thiểu không được lớn hơn tối đa.");

        var query = _context.Receipts
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.ReceiptCode.Contains(keyword) ||
                x.SupplierName.Contains(keyword) ||
                (x.Creator != null &&
                 x.Creator.Fullname.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(
                request.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.ReceiptDate >= from);
        }

        if (request.ToDate.HasValue)
        {
            var until = DateTime.SpecifyKind(
                request.ToDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);
            query = query.Where(x => x.ReceiptDate < until);
        }

        var amountQuery = query.Select(x => new
        {
            Receipt = x,
            Amount = x.ReceiptDetails.Sum(
                d => (decimal?)(d.Quantity * d.ImportPrice)) ?? 0m
        });

        if (request.MinAmount.HasValue)
            amountQuery = amountQuery.Where(
                x => x.Amount >= request.MinAmount.Value);

        if (request.MaxAmount.HasValue)
            amountQuery = amountQuery.Where(
                x => x.Amount <= request.MaxAmount.Value);

        var descending = string.Equals(
            request.SortOrder, "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "date";

        var ordered = sortBy switch
        {
            "code" => descending
                ? amountQuery.OrderByDescending(x => x.Receipt.ReceiptCode)
                : amountQuery.OrderBy(x => x.Receipt.ReceiptCode),

            "supplier" => descending
                ? amountQuery.OrderByDescending(x => x.Receipt.SupplierName)
                : amountQuery.OrderBy(x => x.Receipt.SupplierName),

            "amount" => descending
                ? amountQuery.OrderByDescending(x => x.Amount)
                : amountQuery.OrderBy(x => x.Amount),

            "status" => descending
                ? amountQuery.OrderByDescending(x => x.Receipt.Status)
                : amountQuery.OrderBy(x => x.Receipt.Status),

            "date" => descending
                ? amountQuery.OrderByDescending(x => x.Receipt.ReceiptDate)
                : amountQuery.OrderBy(x => x.Receipt.ReceiptDate),

            _ => throw new ArgumentException(
                "SortBy phải là date, code, supplier, amount hoặc status.")
        };

        var totalItems = await amountQuery.CountAsync();

        var items = await ordered
            .ThenBy(x => x.Receipt.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReceiptListDto(
                x.Receipt.Id,
                x.Receipt.ReceiptCode,
                x.Receipt.SupplierName,
                x.Receipt.CreatedBy,
                x.Receipt.Creator != null
                    ? x.Receipt.Creator.Fullname
                    : string.Empty,
                x.Receipt.ReceiptDate,
                x.Receipt.Status,
                x.Amount))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<ReceiptListDto>(
            items, page, pageSize, totalItems, totalPages);
    }

}
