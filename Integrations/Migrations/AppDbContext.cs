using Integrations.Models;
using Microsoft.EntityFrameworkCore;

namespace Integrations.Migrations;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TransactionModels.Account> Accounts { get; set; }
    public DbSet<TransactionModels.Transaction?> Transactions { get; set; }
    public DbSet<TransactionModels.Customer> Customers { get; set; }
    public DbSet<TransactionModels.OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionModels.Account>()
            .HasIndex(a => a.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<TransactionModels.Transaction>()
            .Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<TransactionModels.Account>()
            .Property(a => a.Balance)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<TransactionModels.OutboxMessage>()
            .HasIndex(o => o.ProcessedAt);
    }
}
