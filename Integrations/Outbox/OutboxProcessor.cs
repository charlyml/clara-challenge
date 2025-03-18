using Integrations.Migrations;
using Integrations.Models;
using Microsoft.EntityFrameworkCore;

namespace Integrations.Outbox;

public class OutboxProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly TransactionModels.IEventPublisher _eventPublisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        AppDbContext dbContext,
        TransactionModels.IEventPublisher eventPublisher,
        ILogger<OutboxProcessor> logger)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task ProcessOutboxMessagesAsync(int batchSize = 100)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(m => !m.IsProcessed && (m.ScheduledAt == null || m.ScheduledAt <= DateTime.UtcNow))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

        foreach (var message in messages)
        {
            try
            {
                // Convert back to event and publish
                var @event = Deserialize(message.EventType, message.EventData);
                if (@event == null) continue;
                await _eventPublisher.PublishAsync(@event);

                message.ProcessedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);

                message.RetryCount++;

                // Implement exponential backoff
                if (message.RetryCount < 10)
                {
                    message.ScheduledAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, message.RetryCount));
                }

                await _dbContext.SaveChangesAsync();
            }
        }
    }


    private TransactionModels.IEvent? Deserialize(string eventType, string eventData)
    {
        var eventTypeInstance = Type.GetType(eventType);
        if (eventTypeInstance == null)
        {
            return null;
        }

        return (TransactionModels.IEvent)System.Text.Json.JsonSerializer.Deserialize(eventData, eventTypeInstance)!;
    }
}
