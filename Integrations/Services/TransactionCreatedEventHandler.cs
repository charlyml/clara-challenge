using Integrations.Models;

namespace Integrations.Services;

public class TransactionCreatedEventHandler :
    TransactionModels.IEventHandler<TransactionModels.TransactionCreatedEvent>
{
    private readonly TransactionModels.IAccountService _accountService;
    private readonly TransactionModels.IFraudDetectionService _fraudDetectionService;
    private readonly ILogger<TransactionCreatedEventHandler> _logger;

    public TransactionCreatedEventHandler(
        TransactionModels.IAccountService accountService,
        TransactionModels.IFraudDetectionService fraudDetectionService,
        ILogger<TransactionCreatedEventHandler> logger)
    {
        _accountService = accountService;
        _fraudDetectionService = fraudDetectionService;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionModels.TransactionCreatedEvent @event)
    {
        if (@event.Transaction != null)
        {
            _logger.LogInformation("Processing transaction {TransactionId}", @event.Transaction.Id);

            bool isFraudulent = await _fraudDetectionService.AnalyzeTransactionAsync(@event.Transaction);

            if (isFraudulent)
            {
                _logger.LogWarning("Potential fraud detected, transaction {TransactionId} flagged",
                    @event.Transaction.Id);
                return;
            }

            bool success = await _accountService.ProcessTransactionAsync(@event.Transaction);

            if (!success)
            {
                _logger.LogWarning("Transaction {TransactionId} processing failed", @event.Transaction.Id);
            }
        }
    }
}
