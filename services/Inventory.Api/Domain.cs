using Microsoft.EntityFrameworkCore;

namespace Inventory.Api
{
    public class InventoryDb : DbContext
    {
        public InventoryDb(DbContextOptions<InventoryDb> opt) : base(opt) { }

        public DbSet<Product> Products => Set<Product>();
    }

    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }
}
