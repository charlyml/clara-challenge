using Integrations.Migrations;
using Integrations.Models;
using Microsoft.EntityFrameworkCore;

namespace Integrations.Services;

public class AccountService : TransactionModels.IAccountService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AccountService> _logger;
    private readonly TransactionModels.IEventPublisher _eventPublisher;

    public AccountService(
        AppDbContext dbContext,
        ILogger<AccountService> logger,
        TransactionModels.IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public Task<TransactionModels.Account> GetAccountAsync(Guid accountId)
    {
        throw new NotImplementedException();
    }

    public Task<decimal> GetBalanceAsync(Guid accountId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ProcessTransactionAsync(TransactionModels.Transaction? transaction)
    {
        await using var transaction1 = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var sourceAccount = await _dbContext.Accounts.FindAsync(transaction.AccountId);
            if (sourceAccount == null)
            {
                transaction.Status = TransactionModels.TransactionStatus.Failed;
                transaction.FailureReason = "Source account not found";
                await _dbContext.Transactions.AddAsync(transaction);
                await _dbContext.SaveChangesAsync();

                await transaction1.CommitAsync();

                await _eventPublisher.PublishAsync(new TransactionModels.TransactionFailedEvent
                {
                    Transaction = transaction,
                    FailureReason = transaction.FailureReason
                });

                return false;
            }

            decimal oldBalance = sourceAccount.Balance;

            switch (transaction.Type)
            {
                case TransactionModels.TransactionType.Deposit:
                    sourceAccount.Balance += transaction.Amount;
                    break;
                case TransactionModels.TransactionType.Withdrawal:
                    if (sourceAccount.Balance < transaction.Amount)
                    {
                        transaction.Status = TransactionModels.TransactionStatus.Failed;
                        transaction.FailureReason = "Insufficient funds";
                        await _dbContext.Transactions.AddAsync(transaction);
                        await _dbContext.SaveChangesAsync();

                        await transaction1.CommitAsync();

                        await _eventPublisher.PublishAsync(new TransactionModels.TransactionFailedEvent
                        {
                            Transaction = transaction,
                            FailureReason = transaction.FailureReason
                        });

                        return false;
                    }

                    sourceAccount.Balance -= transaction.Amount;
                    break;
                case TransactionModels.TransactionType.Transfer:
                    if (sourceAccount.Balance < transaction.Amount)
                    {
                        transaction.Status = TransactionModels.TransactionStatus.Failed;
                        transaction.FailureReason = "Insufficient funds";
                        await _dbContext.Transactions.AddAsync(transaction);
                        await _dbContext.SaveChangesAsync();

                        await transaction1.CommitAsync();

                        await _eventPublisher.PublishAsync(new TransactionModels.TransactionFailedEvent
                        {
                            Transaction = transaction,
                            FailureReason = transaction.FailureReason
                        });

                        return false;
                    }

                    var destinationAccount = await _dbContext.Accounts.FindAsync(transaction.DestinationAccountId);
                    if (destinationAccount == null)
                    {
                        transaction.Status = TransactionModels.TransactionStatus.Failed;
                        transaction.FailureReason = "Destination account not found";
                        await _dbContext.Transactions.AddAsync(transaction);
                        await _dbContext.SaveChangesAsync();

                        await transaction1.CommitAsync();

                        await _eventPublisher.PublishAsync(new TransactionModels.TransactionFailedEvent
                        {
                            Transaction = transaction,
                            FailureReason = transaction.FailureReason
                        });

                        return false;
                    }

                    sourceAccount.Balance -= transaction.Amount;
                    destinationAccount.Balance += transaction.Amount;
                    destinationAccount.LastUpdated = DateTime.UtcNow;
                    break;
            }

            sourceAccount.LastUpdated = DateTime.UtcNow;
            transaction.Status = TransactionModels.TransactionStatus.Completed;
            await _dbContext.Transactions.AddAsync(transaction);
            await _dbContext.SaveChangesAsync();

            // Add to outbox
            var outboxMessage = new TransactionModels.OutboxMessage
            {
                EventType = nameof(TransactionModels.TransactionProcessedEvent),
                EventData = System.Text.Json.JsonSerializer.Serialize(new TransactionModels.TransactionProcessedEvent
                {
                    Transaction = transaction,
                    NewBalance = sourceAccount.Balance
                })
            };
            await _dbContext.OutboxMessages.AddAsync(outboxMessage);

            // Add balance update event to outbox
            var balanceUpdateMessage = new TransactionModels.OutboxMessage
            {
                EventType = nameof(TransactionModels.BalanceUpdatedEvent),
                EventData = System.Text.Json.JsonSerializer.Serialize(new TransactionModels.BalanceUpdatedEvent
                {
                    AccountId = sourceAccount.Id,
                    OldBalance = oldBalance,
                    NewBalance = sourceAccount.Balance,
                    TransactionId = transaction.Id
                })
            };
            await _dbContext.OutboxMessages.AddAsync(balanceUpdateMessage);

            await _dbContext.SaveChangesAsync();
            await transaction1.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            await transaction1.RollbackAsync();
            _logger.LogError(ex, "Error processing transaction {TransactionId}", transaction.Id);

            transaction.Status = TransactionModels.TransactionStatus.Failed;
            transaction.FailureReason = $"Internal error: {ex.Message}";

            // Try to save the failed transaction status
            try
            {
                _dbContext.Entry(transaction).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                await _eventPublisher.PublishAsync(new TransactionModels.TransactionFailedEvent
                {
                    Transaction = transaction,
                    FailureReason = transaction.FailureReason
                });
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save transaction failure state {TransactionId}", transaction.Id);
            }

            return false;
        }
    }

    public Task<bool> LockAccountAsync(Guid accountId, string reason)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UnlockAccountAsync(Guid accountId)
    {
        throw new NotImplementedException();
    }
}
