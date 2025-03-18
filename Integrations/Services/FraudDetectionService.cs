using Integrations.Migrations;
using Integrations.Models;
using Microsoft.EntityFrameworkCore;

namespace Integrations.Services;

public class FraudDetectionService : TransactionModels.IFraudDetectionService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<FraudDetectionService> _logger;
    private readonly TransactionModels.IEventPublisher _eventPublisher;

    public FraudDetectionService(
        AppDbContext dbContext,
        ILogger<FraudDetectionService> logger,
        TransactionModels.IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> AnalyzeTransactionAsync(TransactionModels.Transaction transaction)
    {
        int riskScore = await CalculateRiskScoreAsync(transaction);

        if (riskScore <= 80) return false;

        await _eventPublisher.PublishAsync(new TransactionModels.FraudAlertEvent
        {
            Transaction = transaction,
            AlertReason = "High risk transaction detected",
            RiskScore = riskScore
        });

        return true;
    }

    public Task<int> CalculateRiskScoreAsync(TransactionModels.Transaction transaction)
    {
        return Task.FromResult(transaction.Amount > 1000 ? 100 : 90);
    }

    public async Task ProcessTransactionForFraudAsync(TransactionModels.Transaction transaction)
    {
        var isFraudulent = await AnalyzeTransactionAsync(transaction);

        if (isFraudulent)
        {
            _logger.LogWarning("Potential fraud detected for transaction {TransactionId}", transaction.Id);

            // Update transaction status
            transaction.Status = TransactionModels.TransactionStatus.Flagged;
            _dbContext.Entry(transaction).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }
    }
}
