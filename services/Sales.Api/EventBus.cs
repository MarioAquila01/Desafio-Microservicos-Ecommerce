using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Sales.Api;

public interface IEventBus
{
    void Publish(string routingKey, object message);
}

public sealed class RabbitBus : IEventBus, IDisposable
{
    private readonly IConfiguration _cfg;
    private IConnection? _conn;
    private IModel? _ch;

    public RabbitBus(IConfiguration cfg) => _cfg = cfg;

    private bool EnsureConnection()
    {
        if (_conn is { IsOpen: true } && _ch is { IsOpen: true }) return true;

        var factory = new ConnectionFactory
        {
            HostName = _cfg["RabbitMQ:Host"] ?? "127.0.0.1",
            Port     = int.TryParse(_cfg["RabbitMQ:Port"], out var p) ? p : 5672,
            UserName = _cfg["RabbitMQ:User"] ?? "guest",
            Password = _cfg["RabbitMQ:Pass"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval  = TimeSpan.FromSeconds(5)
        };

        // 2–3 tentativas rápidas
        for (int i = 0; i < 3; i++)
        {
            try
            {
                _conn?.Dispose();
                _ch?.Dispose();
                _conn = factory.CreateConnection();
                _ch   = _conn.CreateModel();
                return true;
            }
            catch
            {
                Thread.Sleep(500);
            }
        }
        return false;
    }

    public void Publish(string queue, object message)
    {
        try
        {
            if (!EnsureConnection())
            {
                Console.WriteLine("[RabbitBus] Sem conexão, ignorando publish (dev).");
                return; // Em DEV: não derruba o fluxo
            }

            _ch!.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            _ch.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitBus] Publish falhou: {ex.Message}");
            // Em PROD: log/retentar/outbox. Em DEV: não falhe a API.
        }
    }

    public void Dispose()
    {
        try { _ch?.Close(); _ch?.Dispose(); } catch {}
        try { _conn?.Close(); _conn?.Dispose(); } catch {}
    }
}