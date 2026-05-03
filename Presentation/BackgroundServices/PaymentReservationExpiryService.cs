using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Presentation;

namespace Presentation.BackgroundServices
{
    public class PaymentReservationExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentReservationExpiryService> _logger;
        private readonly IOptionsMonitor<PaymentReservationOptions> _options;

        public PaymentReservationExpiryService(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentReservationExpiryService> logger,
            IOptionsMonitor<PaymentReservationOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Payment reservation sweep failed");
                }

                var intervalMinutes = Math.Max(1, _options.CurrentValue.SweepIntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task ProcessAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
            var gateways = scope.ServiceProvider.GetServices<IPaymentGateway>();

            var expiryMinutes = Math.Max(1, _options.CurrentValue.ExpiryMinutes);
            var cutoff = DateTime.UtcNow.AddMinutes(-expiryMinutes);

            var pendingPayments = await unitOfWork.Payments.GetPendingOlderThanAsync(cutoff);

            foreach (var payment in pendingPayments)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                var gateway = gateways.FirstOrDefault(g =>
                    g.GatewayName.Equals(payment.GatewayName, StringComparison.OrdinalIgnoreCase));

                if (gateway == null)
                {
                    _logger.LogWarning(
                        "No gateway found for payment {PaymentId} ({Gateway})",
                        payment.Id, payment.GatewayName);
                    continue;
                }

                var statusResult = await gateway.GetPaymentStatusAsync(payment.GatewayPaymentId);
                if (!statusResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Failed to verify payment status for {PaymentId}: {Errors}",
                        payment.Id, string.Join(", ", statusResult.Errors));
                    continue;
                }

                switch (statusResult.Data)
                {
                    case GatewayPaymentStatus.Succeeded:
                        await paymentService.HandlePaymentSucceededAsync(new GatewayWebhookEvent
                        {
                            EventType = GatewayEventTypes.PaymentSucceeded,
                            GatewayPaymentId = payment.GatewayPaymentId,
                            CustomerId = payment.CustomerId,
                            EventId = "expiry_sweep"
                        });
                        break;

                    case GatewayPaymentStatus.Pending:
                    case GatewayPaymentStatus.Failed:
                        await paymentService.HandlePaymentFailedAsync(new GatewayWebhookEvent
                        {
                            EventType = GatewayEventTypes.PaymentFailed,
                            GatewayPaymentId = payment.GatewayPaymentId,
                            CustomerId = payment.CustomerId,
                            EventId = "expiry_sweep"
                        });
                        break;

                    case GatewayPaymentStatus.Unknown:
                    default:
                        _logger.LogInformation(
                            "Skipping expiry for payment {PaymentId} — status unknown",
                            payment.Id);
                        break;
                }
            }
        }
    }
}
