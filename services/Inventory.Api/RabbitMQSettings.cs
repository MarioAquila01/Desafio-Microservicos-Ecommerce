namespace Sales.Api.Configuration;

public class RabbitMQSettings
{
    public string Uri { get; set; } = default!;
    public string ExchangeName { get; set; } = "sales.events"; // Default exchange for sales-related events
    public string OrderConfirmedRoutingKey { get; set; } = "order.confirmed"; // Routing key for OrderConfirmed events
    // Add other specific routing keys or queue names if needed for other events published by Sales.Api
}