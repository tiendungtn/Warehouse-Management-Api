using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;
using System.Security.Claims;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/issues")]
[Authorize]
public sealed class IssuesController : ControllerBase
{
    private readonly IssueService _service;

    public IssuesController(
        IssueService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:issues.view")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(
            await _service.GetAllAsync());
    }

    [HttpGet("search")]
    [Authorize(Policy = "Permission:issues.view")]
    public async Task<IActionResult> Search(
        [FromQuery] IssueSearchRequest request)
    {
        return Ok(await _service.SearchAsync(request));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Permission:issues.view")]
    public async Task<IActionResult> GetById(
        int id)
    {
        return Ok(
            await _service.GetByIdAsync(id));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:issues.create")]
    public async Task<IActionResult> Create(
        IssueRequest request)
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
    [Authorize(Policy = "Permission:issues.approve")]
    public async Task<IActionResult> Approve(
        int id,
        ApproveIssueRequest request)
    {
        var result =
            await _service.ApproveAsync(
                id,
                request.CustomerName);

        return Ok(result);
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