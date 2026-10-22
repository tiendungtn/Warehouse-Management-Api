using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.DTOs;

public sealed record CategoryDto(
    int Id,
    string CategoryName,
    string? Description,
    int ProductCount);

public sealed class CategoryRequest
{
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = "";

    [MaxLength(250)]
    public string? Description { get; set; }
}
public sealed class CategorySearchRequest
{
    public string? Search { get; set; }
    public bool? HasProducts { get; set; }
    public int? MinProductCount { get; set; }
    public int? MaxProductCount { get; set; }

    public string SortBy { get; set; } = "name";
    public string SortOrder { get; set; } = "asc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
