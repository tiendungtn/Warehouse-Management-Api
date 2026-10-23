using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string Fullname { get; set; } = string.Empty;

        // Các quyền của người dùng, ví dụ: "Admin", "Staff", "Manager"
        [Required, MaxLength(20)]
        public string Role { get; set; } = "Staff";
          
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(20)]
        public string PermissionMode { get; set; } = "RoleBased";

        public virtual ICollection<UserPermissionOverride> PermissionOverrides
        { get; set; } = new List<UserPermissionOverride>();

        // navigation property
        public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
        public virtual ICollection<Issue> Issues { get; set; } = new List<Issue>();
    }
}
