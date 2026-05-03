using Domain.Entities;

namespace Domain.Repositories.Interfaces
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        /// <summary>
        /// Looks up a Payment record by the gateway's own payment/session ID.
        /// Works for any gateway (Stripe PaymentIntent ID, Paymob intention ID, etc.).
        /// </summary>
        Task<Payment?> GetByGatewayPaymentIdAsync(string gatewayPaymentId);

        /// <summary>
        /// Fallback: returns the most recent pending Payment for a customer.
        /// Used when a webhook's GatewayPaymentId doesn't match the stored id
        /// (e.g. Paymob sends order.id but we stored the intention id).
        /// </summary>
        Task<Payment?> GetPendingByCustomerIdAsync(string customerId);
    }
}
