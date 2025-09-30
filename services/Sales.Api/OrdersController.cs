using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Contracts.Events;   // DTOs + OrderConfirmed

namespace Sales.Api.Controllers
{
    [ApiController]
    [Route("orders")] // o Gateway remove /sales
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly SalesDb _db;
        private readonly HttpClient _http;
        private readonly IEventBus _bus;
        private readonly ILogger<OrdersController> _logger;

        private sealed record AvailabilityResponse(bool available, int currentStock);

        public OrdersController(SalesDb db, IHttpClientFactory httpFactory, IEventBus bus, ILogger<OrdersController> logger)
        {
            _db = db;
            _http = httpFactory.CreateClient("inventory");
            _bus = bus;
            _logger = logger;
        }

        // POST /orders  (requer role "seller")
        [HttpPost]
        [Authorize(Roles = "seller")]
        public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (userId is null) return Unauthorized();

            // verifica disponibilidade no Inventory via Gateway
            // O prefixo "inventory/" é removido pelo Gateway, não precisa ser incluído aqui.
            var avail = await _http.GetFromJsonAsync<AvailabilityResponse>(
                $"products/{dto.ProductId}/availability?quantity={dto.Quantity}");

            if (avail is null || !avail.available)
                return BadRequest(new { error = "Sem estoque" });

            var order = new Order
            {
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                Status = "Confirmed"
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Pedido {OrderId} criado para o produto {ProductId}", order.Id, order.ProductId);

            // publica evento para o Inventory decrementar estoque
            _bus.Publish("sales.order_confirmed",
                new OrderConfirmed(order.Id, order.ProductId, order.Quantity, DateTime.UtcNow));
            _logger.LogInformation("Evento OrderConfirmed publicado para o pedido {OrderId}", order.Id);

            return CreatedAtAction(nameof(GetById), new { id = order.Id },
                new OrderDto(order.Id, order.ProductId, order.Quantity, order.Status, order.CreatedAtUtc));
        }

        // GET /orders/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<OrderDto>> GetById(Guid id)
        {
            // TODO: Adicionar filtro por UserId para segurança
            var o = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id);
            return o is null
                ? NotFound()
                : Ok(new OrderDto(o.Id, o.ProductId, o.Quantity, o.Status, o.CreatedAtUtc));
        }

        // GET /orders
        [HttpGet]
        public async Task<IEnumerable<OrderDto>> List()
            => await _db.Orders
                .Select(o => new OrderDto(o.Id, o.ProductId, o.Quantity, o.Status, o.CreatedAtUtc))
                .ToListAsync();
    }
}
