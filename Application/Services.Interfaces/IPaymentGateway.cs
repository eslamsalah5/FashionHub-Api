using Application.DTOs.Payment;
using Application.Models;

namespace Application.Services.Interfaces
{
    /// <summary>
    /// Abstraction over any payment gateway (Stripe, PayPal, Paymob, …).
    /// Add a new gateway by implementing this interface — no other code changes needed.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Unique name used to select this gateway from the client request.
        /// e.g. "stripe", "paypal", "paymob"
        /// </summary>
        string GatewayName { get; }

        /// <summary>
        /// Creates a payment session with the gateway and returns the data
        /// the frontend needs to complete the payment (e.g. clientSecret for Stripe.js).
        /// </summary>
        Task<ServiceResult<GatewaySessionResult>> CreateSessionAsync(
            decimal amount, string currency, string customerId);

        /// <summary>
        /// Reads the raw webhook body + headers and returns a normalised event.
        /// Returns Failure if the signature is invalid.
        /// Headers are passed as a plain dictionary to keep the Application layer
        /// independent of ASP.NET Core HTTP types.
        /// </summary>
        Task<ServiceResult<GatewayWebhookEvent>> ParseWebhookAsync(
            string rawBody, IDictionary<string, string> headers);
    }
}
