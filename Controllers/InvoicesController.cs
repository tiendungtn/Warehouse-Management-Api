using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController
    : ControllerBase
{
    private readonly InvoiceService _service;

    public InvoicesController(
        InvoiceService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:invoices.view")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(
            await _service.GetAllAsync());
    }

    [HttpGet("search")]
    [Authorize(Policy = "Permission:invoices.view")]
    public async Task<IActionResult> Search(
        [FromQuery] InvoiceSearchRequest request)
    {
        return Ok(await _service.SearchAsync(request));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Permission:invoices.view")]
    public async Task<IActionResult> GetById(
        int id)
    {
        return Ok(
            await _service.GetByIdAsync(id));
    }

    [HttpGet("{id:int}/pdf")]
    [Authorize(Policy = "Permission:invoices.download")]
    public async Task<IActionResult> GetPdf(
        int id)
    {
        var result =
            await _service.GeneratePdfAsync(id);

        return File(
            result.Content,
            "application/pdf",
            result.FileName);
    }
}