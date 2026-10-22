using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.DTOs;

public sealed record ProductDto(
    int Id,
    string ProductCode,
    string ProductName,
    int CategoryId,
    string CategoryName,
    string Unit,
    decimal Price,
    int StockQuantity);

public sealed class ProductRequest
{
    [Required]
    [MaxLength(50)]
    public string ProductCode { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Unit { get; set; } = "";

    [Range(0, 100000000000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}
public sealed class ProductSearchRequest
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinStock { get; set; }
    public int? MaxStock { get; set; }

    // all | outofstock | lowstock | instock
    public string? StockFilter { get; set; }

    public string SortBy { get; set; } = "name";
    public string SortOrder { get; set; } = "asc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
