
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;

namespace QuanLyKho.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _service;

    public ProductsController(ProductService service)
    {
        _service = service;
    }

    // GET: api/products/search
    [HttpGet("search")]
    [Authorize(Policy = "Permission:products.view")]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchRequest request)
    {
        return Ok(await _service.SearchAsync(request));
    }

    // GET: api/products?search=...
    [HttpGet]
    [Authorize(Policy = "Permission:products.view")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search)
    {
        return Ok(await _service.GetAllAsync(search));
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = "Permission:products.view")]
    public async Task<IActionResult> Get(int id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    // POST: api/products
    [HttpPost]
    [Authorize(Policy = "Permission:products.create")]
    public async Task<IActionResult> Create(
        ProductRequest request)
    {
        var result = await _service.CreateAsync(request);

        return CreatedAtAction(
            nameof(Get),
            new { id = result.Id },
            result);
    }

    // PUT: api/products/5
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Permission:products.edit")]
    public async Task<IActionResult> Update(
        int id,
        ProductRequest request)
    {
        return Ok(await _service.UpdateAsync(id, request));
    }

    // DELETE: api/products/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Permission:products.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);

        return NoContent();
    }
}
