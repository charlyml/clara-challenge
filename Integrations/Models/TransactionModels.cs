namespace Integrations.Models;

public static class TransactionModels
{
    public enum TransactionType
    {
        Deposit,
        Withdrawal,
        Transfer
    }

    public enum TransactionChannel
    {
        Mobile,
        Web,
        ATM,
        Branch
    }

    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed,
        Flagged
    }

    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid? DestinationAccountId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; }
        public TransactionChannel Channel { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public required string Description { get; set; }
        public string? FailureReason { get; set; }
        public required string IpAddress { get; set; }
    }

    public class Account
    {
        public Guid Id { get; set; }
        public required string AccountNumber { get; set; }
        public decimal Balance { get; set; }
        public bool IsLocked { get; set; }
        public DateTime LastUpdated { get; set; }
        public Guid CustomerId { get; set; }
    }

    public class Customer
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public bool OptedForNotifications { get; set; }
    }

    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string EventType { get; set; }
        public required string EventData { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public bool IsProcessed => ProcessedAt.HasValue;
        public int RetryCount { get; set; } = 0;
        public DateTime? ScheduledAt { get; set; }
    }

    public interface IEvent
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
    }

    public class TransactionCreatedEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Transaction? Transaction { get; set; }
    }

    public class TransactionProcessedEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Transaction? Transaction { get; set; }
        public decimal NewBalance { get; set; }
    }

    public class TransactionFailedEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Transaction? Transaction { get; set; }
        public string? FailureReason { get; set; }
    }

    public class FraudAlertEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public required Transaction Transaction { get; set; }
        public required string AlertReason { get; set; }
        public int RiskScore { get; set; }
    }

    public class BalanceUpdatedEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Guid AccountId { get; set; }
        public decimal OldBalance { get; set; }
        public decimal NewBalance { get; set; }
        public Guid TransactionId { get; set; }
    }

    public class NotificationEvent : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Guid CustomerId { get; set; }
        public required string Message { get; set; }
        public NotificationType Type { get; set; }
    }

    public enum NotificationType
    {
        TransactionCompleted,
        LowBalance,
        FraudAlert,
        FailedTransaction
    }

    public interface IEventHandler<T> where T : IEvent
    {
        Task HandleAsync(T @event);
    }

    public interface IEventPublisher
    {
        Task PublishAsync<T>(T @event) where T : IEvent;
    }

    public interface IEventConsumer
    {
        Task ConsumeAsync<T>(Func<T, Task> handler) where T : IEvent;
    }

    public interface IAccountService
    {
        Task<Account> GetAccountAsync(Guid accountId);
        Task<decimal> GetBalanceAsync(Guid accountId);
        Task<bool> ProcessTransactionAsync(Transaction? transaction);
        Task<bool> LockAccountAsync(Guid accountId, string reason);
        Task<bool> UnlockAccountAsync(Guid accountId);
    }

    public interface ITransactionService
    {
        Task<Transaction?> CreateTransactionAsync(Transaction? transaction);
        Task<Transaction?> GetTransactionAsync(Guid transactionId);
        Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(
            Guid accountId,
            DateTime startDate,
            DateTime endDate);
        Task<bool> UpdateTransactionStatusAsync(
            Guid transactionId,
            TransactionStatus status,
            string? reason = null);
    }

    public interface INotificationService
    {
        Task SendNotificationAsync(Guid customerId, string message, NotificationType type);
        Task ProcessNotificationEventAsync(NotificationEvent notificationEvent);
    }

    public interface IFraudDetectionService
    {
        Task<bool> AnalyzeTransactionAsync(Transaction transaction);
        Task<int> CalculateRiskScoreAsync(Transaction transaction);
        Task ProcessTransactionForFraudAsync(Transaction transaction);
    }
}
