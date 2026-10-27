using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/categories")]
[Authorize]
public sealed class CategoriesController : ControllerBase
{
    private readonly CategoryService _service;

    public CategoriesController(CategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:categories.view")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Permission:categories.view")]
    public async Task<IActionResult> Get(int id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [HttpGet("search")]
    [Authorize(Policy = "Permission:categories.view")]
    public async Task<IActionResult> Search(
        [FromQuery] CategorySearchRequest request)
    {
        return Ok(await _service.SearchAsync(request));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:categories.create")]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        var result = await _service.CreateAsync(request);

        return CreatedAtAction(
            nameof(Get),
            new { id = result.Id },
            result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Permission:categories.edit")]
    public async Task<IActionResult> Update(
        int id,
        CategoryRequest request)
    {
        return Ok(await _service.UpdateAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Permission:categories.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
