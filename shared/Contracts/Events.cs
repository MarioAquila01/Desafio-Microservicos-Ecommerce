namespace Contracts.Events;

public record ProductDto(Guid Id, string Name, string Description, decimal Price, int Stock);
public record CreateProductDto(string Name, string Description, decimal Price, int Stock);

public record CreateOrderDto(Guid ProductId, int Quantity);

// 👇 O controller usa CreatedAtUtc: mantenha este campo aqui
public record OrderDto(Guid Id, Guid ProductId, int Quantity, string Status, DateTime CreatedAtUtc);

public record OrderConfirmed(Guid OrderId, Guid ProductId, int Quantity, DateTime ConfirmedAtUtc);
