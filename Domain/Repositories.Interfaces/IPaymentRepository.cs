using Domain.Entities;

namespace Domain.Repositories.Interfaces
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        /// <summary>
        /// Looks up a Payment record by the gateway's own payment/session ID.
        /// Works for any gateway (Stripe PaymentIntent ID, PayPal order ID, etc.).
        /// </summary>
        Task<Payment?> GetByGatewayPaymentIdAsync(string gatewayPaymentId);
    }
}
