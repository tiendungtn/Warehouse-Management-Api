using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyKho.Models
{
    public class IssueDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public virtual Issue? Issue { get; set; }

        [Required]
        public int ProductId { get; set; }
        
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }

        [Required]
        [Display(Name = "Số lượng xuất")]
        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
    }
}
