using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.DTOs;

namespace QuanLyKho.Services;
public sealed class DashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardDto>
        GetAsync()
    {
        var totalProducts =
            await _context.Products
                .CountAsync();

        var totalCategories =
            await _context.Categories
                .CountAsync();

        var totalUsers =
            await _context.Users
                .CountAsync();

        var pendingReceipts =
            await _context.Receipts
                .CountAsync(
                    x => x.Status == "Pending");

        var pendingIssues =
            await _context.Issues
                .CountAsync(
                    x => x.Status == "Pending");

        var inventoryValue =
            await _context.Products
                .Select(
                    x =>
                        (decimal)x.StockQuantity *
                        x.Price)
                .SumAsync();

        var revenue =
            await _context.Invoices
                .Select(x => x.TotalAmount)
                .SumAsync();

        return new DashboardDto(
            totalProducts,
            totalCategories,
            totalUsers,
            pendingReceipts,
            pendingIssues,
            inventoryValue,
            revenue
        );
    }
    public async Task<DashboardAnalyticsDto> GetAnalyticsAsync()
    {
        var now = DateTime.UtcNow;

        var currentMonth = new DateTime(now.Year, now.Month, 1);

        var previousMonth = currentMonth.AddMonths(-1);

        var nextMonth = currentMonth.AddMonths(1);

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(x =>
                x.CreatedDate >= previousMonth &&
                x.CreatedDate < nextMonth)
            .Select(x => new
            {
                x.CreatedDate,
                x.TotalAmount
            })
            .ToListAsync();

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(x =>
                x.ReceiptDate >= previousMonth &&
                x.ReceiptDate < nextMonth &&
                x.Status == "Approved")
            .Select(x => new
            {
                x.ReceiptDate
            })
            .ToListAsync();

        var issues = await _context.Issues
            .AsNoTracking()
            .Where(x =>
                x.IssueDate >= previousMonth &&
                x.IssueDate < nextMonth &&
                x.Status == "Approved")
            .Select(x => new
            {
                x.IssueDate
            })
            .ToListAsync();

        var daysInCurrentMonth =
            DateTime.DaysInMonth(
                currentMonth.Year,
                currentMonth.Month);

        var daysInPreviousMonth =
            DateTime.DaysInMonth(
                previousMonth.Year,
                previousMonth.Month);

        var trends = Enumerable
            .Range(1, daysInCurrentMonth)
            .Select(day =>
            {
                var previousDay = day <= daysInPreviousMonth
                    ? day
                    : 0;

                return new DashboardTrendPointDto(
                    day,

                    invoices
                        .Where(x =>
                            x.CreatedDate.Year == currentMonth.Year &&
                            x.CreatedDate.Month == currentMonth.Month &&
                            x.CreatedDate.Day == day)
                        .Sum(x => x.TotalAmount),

                    invoices
                        .Where(x =>
                            x.CreatedDate.Year == previousMonth.Year &&
                            x.CreatedDate.Month == previousMonth.Month &&
                            x.CreatedDate.Day == previousDay)
                        .Sum(x => x.TotalAmount),

                    receipts.Count(x =>
                        x.ReceiptDate.Year == currentMonth.Year &&
                        x.ReceiptDate.Month == currentMonth.Month &&
                        x.ReceiptDate.Day == day),

                    receipts.Count(x =>
                        x.ReceiptDate.Year == previousMonth.Year &&
                        x.ReceiptDate.Month == previousMonth.Month &&
                        x.ReceiptDate.Day == previousDay),

                    issues.Count(x =>
                        x.IssueDate.Year == currentMonth.Year &&
                        x.IssueDate.Month == currentMonth.Month &&
                        x.IssueDate.Day == day),

                    issues.Count(x =>
                        x.IssueDate.Year == previousMonth.Year &&
                        x.IssueDate.Month == previousMonth.Month &&
                        x.IssueDate.Day == previousDay)
                );
            })
            .ToList();

        return new DashboardAnalyticsDto(
            currentMonth,
            previousMonth,
            trends
        );
    }
}
