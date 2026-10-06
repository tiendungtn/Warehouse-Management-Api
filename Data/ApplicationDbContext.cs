using Microsoft.EntityFrameworkCore;
using QuanLyKho.Models;

namespace QuanLyKho.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }
        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<ReceiptDetail> ReceiptDetails => Set<ReceiptDetail>();
        public DbSet<Issue> Issues => Set<Issue>();
        public DbSet<IssueDetail> IssueDetails => Set<IssueDetail>();
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure relationships and constraints if needed
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Product>().HasIndex(p => p.ProductCode).IsUnique();
            modelBuilder.Entity<Receipt>().HasIndex(r => r.ReceiptCode).IsUnique();
            modelBuilder.Entity<Issue>().HasIndex(i => i.IssueCode).IsUnique();
            modelBuilder.Entity<Invoice>().HasIndex(i => i.InvoiceCode).IsUnique();


            modelBuilder.Entity<Receipt>()
                .HasOne(r => r.Creator)
                .WithMany(r => r.Receipts)
                .HasForeignKey(rd => rd.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Issue>()
                .HasOne(i => i.Creator)
                .WithMany(i => i.Issues)
                .HasForeignKey(i => i.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IssueDetail>()
                .Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ReceiptDetail>()
                .Property(x => x.ImportPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(x => x.TotalAmount)
                .HasPrecision(18, 2);

        }
    }
}

