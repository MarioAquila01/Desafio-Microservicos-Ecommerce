using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Contracts.Events;

namespace Inventory.Api.Controllers
{
    [ApiController]
    [Route("products")] // o gateway remove o prefixo /inventory
    public class ProductsController : ControllerBase
    {
        private readonly InventoryDb _db;
        public ProductsController(InventoryDb db) { _db = db; }

        // GET /products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> List()
            => Ok(await _db.Products
                .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock))
                .ToListAsync());

        // GET /products/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDto>> GetById(Guid id)
        {
            var p = await _db.Products.FindAsync(id);
            return p is null
                ? NotFound()
                : Ok(new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock));
        }

        // GET /products/{id}/availability?quantity=1
        [HttpGet("{id:guid}/availability")]
        public async Task<ActionResult> Availability(Guid id, [FromQuery] int quantity = 1)
        {
            var p = await _db.Products.FindAsync(id);
            if (p is null) return NotFound();
            return Ok(new { available = p.Stock >= quantity, currentStock = p.Stock });
        }

        // POST /products  (requer role "seller")
        [HttpPost]
        [Authorize(Roles = "seller")]
        public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto)
        {
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = product.Id },
                new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock));
        }
    }
}
