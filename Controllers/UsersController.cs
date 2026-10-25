using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.DTOs;
using QuanLyKho.Services;
using System.Security.Claims;

namespace QuanLyKho.Controllers;
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController
    : ControllerBase
{
    private readonly UserService _service;
    private readonly PermissionService _permissionService;

    public UsersController(
        UserService service,
        PermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(
            await _service.GetAllAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request)
    {
        var result =
            await _service.CreateAsync(
                request);

        return Created(
            $"api/users/{result.Id}",
            result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id,
    UpdateUserRequest request)
    {
        var currentUserId = GetCurrentUserId();

        return Ok(await _service.UpdateAsync(
            id,
            request,
            currentUserId));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var currentUserId =
            GetCurrentUserId();

        await _service.DeleteAsync(
            id,
            currentUserId);

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(
                value,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "Token không hợp lệ.");
        }

        return userId;
    }

    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetPermissions(
    int id,
    CancellationToken cancellationToken)
    {
        var permissions =
    await _permissionService.GetEffectivePermissionsAsync(id);

        return Ok(permissions);
    }

    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> UpdatePermissions(
        int id,
        UpdateUserPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        await _permissionService.SetOverridesAsync(
            id,
            request.PermissionMode,
            request.PermissionOverrides,
            currentUserId,
            cancellationToken);

        return NoContent();
    }
}