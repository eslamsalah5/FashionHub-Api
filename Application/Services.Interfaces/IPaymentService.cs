using Application.DTOs.Payment;
using Application.Models;

namespace Application.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(CreatePaymentIntentDto dto, string customerId);

        /// <summary>
        /// Called by Stripe webhook when payment_intent.succeeded event is received.
        /// Creates the order and clears the cart automatically.
        /// </summary>
        Task<ServiceResult<int>> HandlePaymentSucceededAsync(string paymentIntentId);

        /// <summary>
        /// Called by Stripe webhook when payment_intent.payment_failed event is received.
        /// Marks the payment as failed.
        /// </summary>
        Task<ServiceResult<bool>> HandlePaymentFailedAsync(string paymentIntentId);
    }
}
