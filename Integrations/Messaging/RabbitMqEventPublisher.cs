using System.Text;
using System.Text.Json;
using Integrations.Models;
using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client;

namespace Integrations.Messaging;

public class RabbitMqEventPublisher : TransactionModels.IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private const string ExchangeName = "bank_events";

    public RabbitMqEventPublisher(ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ connection");
            throw;
        }
    }

    public async Task PublishAsync<T>(T @event) where T : TransactionModels.IEvent
    {
        var eventName = @event.GetType().Name;
        var routingKey = eventName.Replace("Event", "").ToLower();

        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        try
        {
            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body);

            _logger.LogInformation("Published event {EventName} with ID {@EventId}", eventName, @event.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventName}", eventName);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}


public class RabbitMqEventConsumer : TransactionModels.IEventConsumer, IHostedService, IDisposable
{
    private readonly ILogger<RabbitMqEventConsumer> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "bank_events";
    private readonly Dictionary<string, string> _queueBindings = new Dictionary<string, string>();

    public RabbitMqEventConsumer(ILogger<RabbitMqEventConsumer> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ connection");
            throw;
        }
    }

    public async Task ConsumeAsync<T>(Func<T, Task> handler) where T : TransactionModels.IEvent
    {
        var eventName = typeof(T).Name;
        var routingKey = eventName.Replace("Event", "").ToLower();
        var queueName = $"queue_{routingKey}";

        // Declare queue
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Bind queue to exchange
        _channel.QueueBind(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: routingKey);

        // Store binding info for StartAsync
        _queueBindings[queueName] = eventName;

        await Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in _queueBindings)
        {
            var queueName = binding.Key;
            var eventName = binding.Value;

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;

                try
                {
                    _logger.LogInformation("Received message for {EventName}", eventName);

                    // Here we would deserialize the message and call the appropriate handler
                    // This is a simplified implementation

                    // Acknowledge the message
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message for {EventName}", eventName);

                    // Reject and requeue the message
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }

                await Task.CompletedTask;
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
