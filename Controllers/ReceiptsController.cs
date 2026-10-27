using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;
using System.Security.Claims;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/receipts")]
[Authorize]
public sealed class ReceiptsController : ControllerBase
{
    private readonly ReceiptService _service;

    public ReceiptsController(
        ReceiptService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:receipts.view")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(
            await _service.GetAllAsync());
    }

    [HttpGet("search")]
    [Authorize(Policy = "Permission:receipts.view")]
    public async Task<IActionResult> Search(
        [FromQuery] ReceiptSearchRequest request)
    {
        return Ok(await _service.SearchAsync(request));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Permission:receipts.view")]
    public async Task<IActionResult> GetById(int id)
    {
        return Ok(
            await _service.GetByIdAsync(id));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:receipts.create")]
    public async Task<IActionResult> Create(
        ReceiptRequest request)
    {
        var userId = GetCurrentUserId();

        var result =
            await _service.CreateAsync(
                request,
                userId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = "Permission:receipts.approve")]
    public async Task<IActionResult> Approve(int id)
    {
        await _service.ApproveAsync(id);

        return Ok(new
        {
            message = "Duyệt phiếu nhập thành công."
        });
    }

    private int GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Token không hợp lệ.");
        }

        return userId;
    }
}
