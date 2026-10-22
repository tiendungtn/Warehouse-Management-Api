using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController
    : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(
        ReportService service)
    {
        _service = service;
    }

    [HttpGet("stock")]
    [Authorize(Roles =
        "Admin,Manager,Staff")]
    public async Task<IActionResult> Stock(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] string? stockFilter)
    {
        return Ok(
            await _service.GetStockReportAsync(
                search,
                categoryId,
                stockFilter));
    }

    [HttpGet("stock/export")]
    [Authorize(Roles =
        "Admin,Manager")]
    public async Task<IActionResult>
        ExportStock()
    {
        var result =
            await _service
                .ExportStockCsvAsync();

        return File(
            result.Content,
            "text/csv; charset=utf-8",
            result.FileName);
    }

    [HttpGet("revenue")]
    [Authorize(Roles =
        "Admin,Manager")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        return Ok(
            await _service
                .GetRevenueReportAsync(
                    fromDate,
                    toDate));
    }

    [HttpGet("stock/search")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> SearchStock(
        [FromQuery] StockSearchRequest request)
    {
        var summary = await _service.GetStockSummaryAsync(request);
        var result = await _service.SearchStockAsync(request);

        return Ok(new
        {
            totalProductCount = summary.TotalProductCount,
            totalStockQuantity = summary.TotalStockQuantity,
            totalStockValuation = summary.TotalStockValuation,
            outOfStockCount = summary.OutOfStockCount,
            lowStockCount = summary.LowStockCount,
            products = result.Items,
            page = result.Page,
            pageSize = result.PageSize,
            totalItems = result.TotalItems,
            totalPages = result.TotalPages
        });
    }

}