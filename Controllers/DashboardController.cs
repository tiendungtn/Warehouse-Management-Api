using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Services;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController
    : ControllerBase
{
    private readonly DashboardService _service;

    public DashboardController(
        DashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(
            await _service.GetAsync());
    }
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics()
    {
        return Ok(await _service.GetAnalyticsAsync());
    }
}
