using Microsoft.EntityFrameworkCore;

namespace Sales.Api
{
    public class SalesDb : DbContext
    {
        public SalesDb(DbContextOptions<SalesDb> opt) : base(opt) { }
        public DbSet<Order> Orders => Set<Order>();
    }

    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
