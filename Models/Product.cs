using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace QuanLyKho.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "Mã hàng hoá")]
        public String ProductCode { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        [Display(Name = "Tên hàng hoá")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tên Danh mục")]
        public int CategoryId { get; set; }

        [ForeignKey (nameof(CategoryId)) ]
        public virtual Category? Category { get; set; }

        [Required, MaxLength(30)]
        [Display(Name = "Đơn vị tính")]
        public string Unit { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Giá")]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Display(Name = "Số lượng tồn kho")]
        [Range(0, double.MaxValue)]
        public int StockQuantity { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<ReceiptDetail> ReceiptDetails { get; set; } = new List<ReceiptDetail>();
        public virtual ICollection<IssueDetail> IssueDetails { get; set; } = new List<IssueDetail>();
    }
}
