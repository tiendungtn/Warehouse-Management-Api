namespace QuanLyKho.DTOs;

public sealed record StockReportItemDto(
    int ProductId,
    string ProductCode,
    string ProductName,
    string CategoryName,
    string Unit,
    decimal Price,
    int StockQuantity,
    decimal TotalValuation,
    string Status);

public sealed record StockReportDto(
    int TotalProductCount,
    int TotalStockQuantity,
    decimal TotalStockValuation,
    int OutOfStockCount,
    int LowStockCount,
    IReadOnlyList<StockReportItemDto> Products);

public sealed record RevenueReportDto(
    DateTime? FromDate,
    DateTime? ToDate,
    decimal TotalRevenue,
    IReadOnlyList<InvoiceListDto> Invoices);

public sealed record DashboardDto(
    int TotalProducts,
    int TotalCategories,
    int TotalUsers,
    int PendingReceipts,
    int PendingIssues,
    decimal InventoryValue,
    decimal Revenue);

public sealed record RevenueProductDto(
    int ProductId,
    string ProductCode,
    string ProductName,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public sealed record RevenueInvoiceDetailDto(
    int Id,
    string InvoiceCode,
    string IssueCode,
    string CustomerName,
    DateTime CreatedDate,
    decimal TotalAmount,
    IReadOnlyList<RevenueProductDto> Items
);

public sealed record RevenueMonthDto(
    int Year,
    int Month,
    string MonthLabel,
    decimal TotalRevenue,
    int InvoiceCount,
    decimal AverageInvoiceValue,
    decimal? ChangePercent,
    IReadOnlyList<RevenueInvoiceDetailDto> Invoices
);

public sealed record MonthlyRevenueReportDto(
    DateTime? FromDate,
    DateTime? ToDate,
    decimal TotalRevenue,
    int InvoiceCount,
    decimal AverageInvoiceValue,
    IReadOnlyList<RevenueMonthDto> Months
);