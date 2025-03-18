using Integrations.Migrations;
using Integrations.Models;
using Microsoft.EntityFrameworkCore;

namespace Integrations.Services;

public class TransactionService : TransactionModels.ITransactionService
{
    private readonly AppDbContext _dbContext;
    private readonly TransactionModels.IEventPublisher _eventPublisher;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        AppDbContext dbContext,
        TransactionModels.IEventPublisher eventPublisher,
        ILogger<TransactionService> logger)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<TransactionModels.Transaction?> CreateTransactionAsync(TransactionModels.Transaction? transaction)
    {
        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _dbContext.Transactions.AddAsync(transaction);
            await _dbContext.SaveChangesAsync();

            // Add to outbox instead of direct publishing
            var outboxMessage = new TransactionModels.OutboxMessage
            {
                EventType = nameof(TransactionModels.TransactionCreatedEvent),
                EventData = System.Text.Json.JsonSerializer.Serialize(new TransactionModels.TransactionCreatedEvent
                {
                    Transaction = transaction
                })
            };
            await _dbContext.OutboxMessages.AddAsync(outboxMessage);
            await _dbContext.SaveChangesAsync();

            await dbTransaction.CommitAsync();
            return transaction;
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error creating transaction");
            throw;
        }
    }

    public async Task<TransactionModels.Transaction?> GetTransactionAsync(Guid transactionId)
    {
        return await _dbContext.Transactions.FindAsync(transactionId);
    }

    public async Task<IEnumerable<TransactionModels.Transaction>> GetAccountTransactionsAsync(Guid accountId, DateTime startDate, DateTime endDate)
    {
        return await _dbContext.Transactions
            .Where(t => t.AccountId == accountId && t.Timestamp >= startDate && t.Timestamp <= endDate)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();
    }

    public Task<bool> UpdateTransactionStatusAsync(Guid transactionId, TransactionModels.TransactionStatus status, string? reason = null)
    {
        throw new NotImplementedException();
    }
}
