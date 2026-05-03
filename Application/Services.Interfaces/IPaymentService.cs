using Application.DTOs.Payment;
using Application.Models;

namespace Application.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(CreatePaymentIntentDto dto, string customerId);

        /// <summary>
        /// Called by any gateway webhook when a payment succeeds.
        /// Creates the order, clears the cart, and decrements stock automatically.
        /// The <paramref name="webhookEvent"/> may include a CustomerId as a fallback
        /// lookup strategy for gateways (e.g. Paymob) whose GatewayPaymentId in the
        /// webhook differs from the id stored at intent creation.
        /// </summary>
        Task<ServiceResult<int>> HandlePaymentSucceededAsync(GatewayWebhookEvent webhookEvent);

        /// <summary>
        /// Called by any gateway webhook when a payment fails.
        /// Marks the payment record as failed.
        /// </summary>
        Task<ServiceResult<bool>> HandlePaymentFailedAsync(GatewayWebhookEvent webhookEvent);
    }
}
