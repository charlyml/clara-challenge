using Integrations.Migrations;
using Integrations.Migrations.DatabaseSetup;
using Integrations.Models;
using Integrations.Outbox;
using Integrations.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TransactionModels.IAccountService, AccountService>();
builder.Services.AddScoped<TransactionModels.ITransactionService, TransactionService>();
builder.Services.AddScoped<TransactionModels.INotificationService, NotificationService>();
builder.Services.AddScoped<TransactionModels.IFraudDetectionService, FraudDetectionService>();

builder.Services.AddScoped<TransactionModels.IEventHandler<TransactionModels.TransactionCreatedEvent>, TransactionCreatedEventHandler>();
builder.Services.AddScoped<TransactionModels.IEventHandler<TransactionModels.BalanceUpdatedEvent>, BalanceUpdatedEventHandler>();

builder.Services.AddHostedService<OutboxProcessorService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<TransactionModels.IEventConsumer>() as RabbitMqEventConsumer);

builder.Services.AddScoped<OutboxProcessor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

await InitializeDatabase.InitializeDatabaseAsync(app.Services);
