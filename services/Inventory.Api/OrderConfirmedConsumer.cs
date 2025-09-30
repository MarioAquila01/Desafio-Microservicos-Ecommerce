using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Inventory.Api
{
    public class OrderConfirmedConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderConfirmedConsumer> _logger;
        private readonly IConfiguration _cfg;
        private IConnection? _conn;
        private IModel? _ch;

        public OrderConfirmedConsumer(IServiceScopeFactory scopeFactory, IConfiguration cfg, ILogger<OrderConfirmedConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _cfg = cfg;
        }

        private void EnsureChannel()
        {
            if (_conn is { IsOpen: true } && _ch is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = _cfg["RabbitMQ:Host"] ?? "127.0.0.1",
                Port = int.TryParse(_cfg["RabbitMQ:Port"], out var p) ? p : 5672,
                UserName = _cfg["RabbitMQ:User"] ?? "guest",
                Password = _cfg["RabbitMQ:Pass"] ?? "guest",
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _conn?.Dispose();
            _ch?.Dispose();

            _logger.LogInformation("Conectando ao RabbitMQ em {Host}:{Port}...", factory.HostName, factory.Port);
            _conn = factory.CreateConnection();
            _ch = _conn.CreateModel();

            _ch.QueueDeclare(
                queue: "sales.order_confirmed",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _logger.LogInformation("Conexão estabelecida e fila 'sales.order_confirmed' declarada.");
        }

        private bool EnsureChannelSafe()
        {
            try
            {
                EnsureChannel(); // sua função atual que cria _conn e _ch e declara a fila
                return _conn is { IsOpen: true } && _ch is { IsOpen: true };
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogWarning(ex, "RabbitMQ indisponível. Nova tentativa em 5s...");
                return false;
            }
            catch (OperationInterruptedException ex)
            {
                _logger.LogWarning(ex, "Conexão/Canal interrompido. Nova tentativa em 5s...");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha inesperada ao criar canal.");
                return false;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Loop de vida: mantém o consumidor ativo enquanto o serviço estiver rodando
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tenta conectar/abrir canal; se falhar, aguarda e tenta de novo
                    if (!EnsureChannelSafe())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    // Segurança extra: só prossiga se o canal estiver realmente aberto
                    if (!(_ch?.IsOpen ?? false))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    // (opcional) QoS para processar 1 msg por vez
                    _ch.BasicQos(0, 1, false);

                    var consumer = new AsyncEventingBasicConsumer(_ch!);
                    consumer.Received += async (_, ea) =>
                    {
                        try
                        {
                            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                            var evt = System.Text.Json.JsonSerializer
                                .Deserialize<Contracts.Events.OrderConfirmed>(json);

                            if (evt is null)
                            {
                                _logger.LogWarning("Mensagem inválida (null). Ack.");
                                _ch!.BasicAck(ea.DeliveryTag, false);
                                return;
                            }

                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<InventoryDb>();

                            var prod = await db.Products
                                .FirstOrDefaultAsync(p => p.Id == evt.ProductId, stoppingToken);

                            if (prod is not null)
                            {
                                prod.Stock = Math.Max(0, prod.Stock - evt.Quantity);
                                await db.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Estoque atualizado: {ProductId} => {Stock}", prod.Id, prod.Stock);
                            }
                            else
                            {
                                _logger.LogWarning("Produto {ProductId} não encontrado. Ack.", evt.ProductId);
                            }

                            _ch!.BasicAck(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao processar mensagem. NACK sem requeue.");
                            _ch?.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                        }
                    };

                    // ⚠️ Só consome se _ch estiver aberto
                    if (_ch.IsOpen)
                        _ch.BasicConsume(queue: "sales.order_confirmed", autoAck: false, consumer: consumer);

                    // Fica “parado” até cancelarem; se a conexão cair, o canal dispara exceções e o catch abaixo trata
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Encerramento normal (Ctrl+C / host shutting down)
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Loop do consumidor falhou; tentando reestabelecer em 5s...");
                    try { _ch?.Dispose(); } catch { }
                    try { _conn?.Dispose(); } catch { }
                    _ch = null; _conn = null;
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        public override void Dispose()
        {
            try { _ch?.Dispose(); } catch { }
            try { _conn?.Dispose(); } catch { }
            base.Dispose();
        }
    }
}
