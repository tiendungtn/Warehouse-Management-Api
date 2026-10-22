using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QuanLyKho.Services;
public sealed class InvoiceService
{
    private readonly ApplicationDbContext _context;

    public InvoiceService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InvoiceListDto>>
        GetAllAsync()
    {
        return await _context.Invoices
            .AsNoTracking()
            .Include(x => x.Issue)
            .OrderByDescending(
                x => x.CreatedDate)
            .Select(x => new InvoiceListDto(
                x.Id,
                x.InvoiceCode,
                x.IssueId,
                x.Issue!.IssueCode,
                x.CustomerName,
                x.TotalAmount,
                x.CreatedDate
            ))
            .ToListAsync();
    }

    public async Task<InvoiceDto> GetByIdAsync(
        int id)
    {
        var invoice =
            await _context.Invoices
                .AsNoTracking()
                .Include(x => x.Issue)
                    .ThenInclude(x =>
                        x!.Creator)
                .Include(x => x.Issue)
                    .ThenInclude(x =>
                        x!.IssueDetails)
                    .ThenInclude(x =>
                        x.Product)
                .FirstOrDefaultAsync(
                    x => x.Id == id);

        if (invoice == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy hóa đơn.");
        }

        var issue = invoice.Issue!;

        var items =
            issue.IssueDetails
                .Select(detail =>
                    new InvoiceItemDto(
                        detail.Product?.ProductCode
                            ?? string.Empty,

                        detail.Product?.ProductName
                            ?? string.Empty,

                        detail.Product?.Unit
                            ?? string.Empty,

                        detail.Quantity,

                        detail.Product?.Price
                            ?? 0,

                        detail.Quantity *
                            (detail.Product?.Price
                                ?? 0)
                    ))
                .ToList();

        return new InvoiceDto(
            invoice.Id,
            invoice.InvoiceCode,
            invoice.IssueId,
            issue.IssueCode,
            invoice.CustomerName,
            issue.Creator?.Fullname
                ?? string.Empty,
            invoice.TotalAmount,
            invoice.CreatedDate,
            items
        );
    }

    public async Task<(
        byte[] Content,
        string FileName
    )> GeneratePdfAsync(int id)
    {
        var invoice =
            await GetByIdAsync(id);

        var document = Document.Create(
            container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);

                    page.Margin(
                        1.5f,
                        Unit.Centimetre);

                    page.DefaultTextStyle(
                        x => x.FontSize(11));

                    page.Header().Column(
                        column =>
                        {
                            column.Item()
                                .Text(
                                    "HỆ THỐNG " +
                                    "QUẢN LÝ KHO HÀNG")
                                .Bold()
                                .FontSize(16);

                            column.Item()
                                .Text(
                                    $"HÓA ĐƠN BÁN HÀNG " +
                                    $"({invoice.InvoiceCode})")
                                .SemiBold()
                                .FontSize(14);

                            column.Item()
                                .Text(
                                    $"Ngày lập: " +
                                    $"{invoice.CreatedDate:dd/MM/yyyy HH:mm}");

                            column.Item()
                                .Text(
                                    $"Khách hàng: " +
                                    $"{invoice.CustomerName}");

                            column.Item()
                                .Text(
                                    $"Phiếu xuất: " +
                                    $"{invoice.IssueCode} " +
                                    $"(Nhân viên: " +
                                    $"{invoice.CreatorName})");

                            column.Item()
                                .LineHorizontal(1);
                        });

                    page.Content()
                        .PaddingVertical(10)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(
                                columns =>
                                {
                                    columns
                                    .ConstantColumn(30);

                                    columns
                                    .RelativeColumn(3);

                                    columns
                                    .RelativeColumn(1);

                                    columns
                                    .RelativeColumn(1);

                                    columns
                                    .RelativeColumn(2);

                                    columns
                                    .RelativeColumn(2);
                                });

                            table.Header(header =>
                            {
                                header.Cell()
                                    .Text("#")
                                    .Bold();

                                header.Cell()
                                    .Text("Tên hàng")
                                    .Bold();

                                header.Cell()
                                    .Text("ĐVT")
                                    .Bold();

                                header.Cell()
                                    .Text("SL")
                                    .Bold();

                                header.Cell()
                                    .Text("Đơn giá")
                                    .Bold();

                                header.Cell()
                                    .Text("Thành tiền")
                                    .Bold();
                            });

                            for (
                                var index = 0;
                                index < invoice.Items.Count;
                                index++)
                            {
                                var item =
                                    invoice.Items[index];

                                table.Cell()
                                    .Text(
                                        (index + 1)
                                        .ToString());

                                table.Cell()
                                    .Text(
                                        item.ProductName);

                                table.Cell()
                                    .Text(
                                        item.Unit);

                                table.Cell()
                                    .Text(
                                        item.Quantity
                                            .ToString());

                                table.Cell()
                                    .Text(
                                        $"{item.UnitPrice:N0} đ");

                                table.Cell()
                                    .Text(
                                        $"{item.TotalAmount:N0} đ");
                            }
                        });

                    page.Footer().Column(
                        column =>
                        {
                            column.Item()
                                .LineHorizontal(1);

                            column.Item()
                                .AlignRight()
                                .Text(
                                    $"TỔNG TIỀN THANH TOÁN: " +
                                    $"{invoice.TotalAmount:N0} VNĐ")
                                .Bold()
                                .FontSize(13);
                        });
                });
            });

        return (
            document.GeneratePdf(),
            $"Invoice_{invoice.InvoiceCode}.pdf"
        );
    }

    public async Task<PagedResult<InvoiceListDto>> SearchAsync(
        InvoiceSearchRequest request)
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

        var query = _context.Invoices
            .AsNoTracking()
            .AsQueryable();

        var keyword = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.InvoiceCode.Contains(keyword) ||
                x.CustomerName.Contains(keyword) ||
                (x.Issue != null &&
                 x.Issue.IssueCode.Contains(keyword)));
        }

        if (request.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(
                request.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedDate >= from);
        }

        if (request.ToDate.HasValue)
        {
            var until = DateTime.SpecifyKind(
                request.ToDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);
            query = query.Where(x => x.CreatedDate < until);
        }

        if (request.MinAmount.HasValue)
            query = query.Where(
                x => x.TotalAmount >= request.MinAmount.Value);

        if (request.MaxAmount.HasValue)
            query = query.Where(
                x => x.TotalAmount <= request.MaxAmount.Value);

        var descending = string.Equals(
            request.SortOrder, "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "date";

        var ordered = sortBy switch
        {
            "code" => descending
                ? query.OrderByDescending(x => x.InvoiceCode)
                : query.OrderBy(x => x.InvoiceCode),

            "customer" => descending
                ? query.OrderByDescending(x => x.CustomerName)
                : query.OrderBy(x => x.CustomerName),

            "amount" => descending
                ? query.OrderByDescending(x => x.TotalAmount)
                : query.OrderBy(x => x.TotalAmount),

            "date" => descending
                ? query.OrderByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.CreatedDate),

            _ => throw new ArgumentException(
                "SortBy phải là date, code, customer hoặc amount.")
        };

        var totalItems = await query.CountAsync();

        var items = await ordered
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InvoiceListDto(
                x.Id,
                x.InvoiceCode,
                x.IssueId,
                x.Issue != null ? x.Issue.IssueCode : string.Empty,
                x.CustomerName,
                x.TotalAmount,
                x.CreatedDate))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        return new PagedResult<InvoiceListDto>(
            items, page, pageSize, totalItems, totalPages);
    }

}
