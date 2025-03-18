using Integrations.Migrations;
using Integrations.Models;

namespace Integrations.Services;

public class NotificationService : TransactionModels.INotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext dbContext, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }


    public async Task SendNotificationAsync(
        Guid customerId,
        string message,
        TransactionModels.NotificationType type)
    {
        var customer = await _dbContext.Customers.FindAsync(customerId);
        if (customer == null || !customer.OptedForNotifications)
        {
            _logger.LogInformation("Notification not sent. Customer {CustomerId} not found or opted out", customerId);
            return;
        }

        // In a real implementation, this would send via email, SMS, push notification, etc.
        _logger.LogInformation("Sending {NotificationType} notification to customer {CustomerId}: {Message}",
            type, customerId, message);
    }

    public async Task ProcessNotificationEventAsync(
        TransactionModels.NotificationEvent notificationEvent)
    {
        await SendNotificationAsync(
            notificationEvent.CustomerId,
            notificationEvent.Message,
            notificationEvent.Type);
    }
}
