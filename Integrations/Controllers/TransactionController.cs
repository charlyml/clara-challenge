using System.ComponentModel.DataAnnotations;
using Integrations.Models;
using Microsoft.AspNetCore.Mvc;

namespace Integrations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{

}

public class CreateTransactionRequest
{
    [Required]
    public Guid AccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public TransactionModels.TransactionType Type { get; set; }

    [Required]
    public TransactionModels.TransactionChannel Channel { get; set; }

    public string? Description { get; set; }
}
