using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Stripe;

namespace Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly PaymentIntentService _paymentIntentService;

        public PaymentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _paymentIntentService = new PaymentIntentService();
        }

        // ─────────────────────────────────────────────────────────────
        // Step 1 – Client calls this to get a clientSecret for Stripe.js
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(
            CreatePaymentIntentDto dto, string customerId)
        {
            try
            {
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customerId);
                if (cart == null)
                    return ServiceResult<PaymentIntentResponseDto>.Failure("Cart not found");

                if (!cart.CartItems.Any())
                    return ServiceResult<PaymentIntentResponseDto>.Failure("Cart is empty");

                decimal totalAmount = cart.CartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(totalAmount * 100), // Stripe works in cents
                    Currency = "usd",
                    PaymentMethodTypes = new List<string> { "card" },
                    // Store customerId in Stripe metadata so we can cross-check in webhook
                    Metadata = new Dictionary<string, string>
                    {
                        { "customerId", customerId }
                    }
                };

                var paymentIntent = await _paymentIntentService.CreateAsync(options);

                // Persist a pending payment record — includes customerId so the
                // webhook handler can create the order without trusting the client.
                var payment = new Payment
                {
                    Amount        = totalAmount,
                    StripePaymentIntentId = paymentIntent.Id,
                    Status        = "pending",
                    CustomerId    = customerId
                };

                await _unitOfWork.Payments.AddAsync(payment);
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<PaymentIntentResponseDto>.Success(new PaymentIntentResponseDto
                {
                    ClientSecret = paymentIntent.ClientSecret,
                    Amount       = totalAmount
                });
            }
            catch (StripeException ex)
            {
                return ServiceResult<PaymentIntentResponseDto>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentIntentResponseDto>.Failure($"Error creating payment intent: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Step 2 – Called ONLY by the Stripe webhook (payment_intent.succeeded)
        // The client never calls this directly.
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<int>> HandlePaymentSucceededAsync(string paymentIntentId)
        {
            try
            {
                var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(paymentIntentId);
                if (payment == null)
                    return ServiceResult<int>.Failure("Payment record not found");

                // Idempotency guard — webhook may fire more than once
                if (payment.Status == "succeeded")
                    return ServiceResult<int>.Success(0);

                payment.Status      = "succeeded";
                payment.PaymentDate = DateTime.UtcNow;

                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(payment.CustomerId);
                if (cart == null || !cart.CartItems.Any())
                    return ServiceResult<int>.Failure("Cart not found or already cleared");

                var order = new Order
                {
                    CustomerId  = payment.CustomerId,
                    TotalAmount = payment.Amount,
                    Status      = OrderStatus.Processing,
                    Payment     = payment
                };

                foreach (var cartItem in cart.CartItems)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId    = cartItem.ProductId,
                        ProductName  = cartItem.Product.Name,
                        ProductSKU   = cartItem.Product.SKU,
                        UnitPrice    = cartItem.PriceAtAddition,
                        Quantity     = cartItem.Quantity,
                        Subtotal     = cartItem.Quantity * cartItem.PriceAtAddition,
                        SelectedSize  = cartItem.SelectedSize,
                        SelectedColor = cartItem.SelectedColor
                    });

                    // Deduct stock
                    cartItem.Product.StockQuantity -= cartItem.Quantity;
                }

                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.Carts.ClearCartAsync(cart.Id);
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<int>.Success(order.Id);
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.Failure($"Error handling payment succeeded: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Step 3 – Called ONLY by the Stripe webhook (payment_intent.payment_failed)
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<bool>> HandlePaymentFailedAsync(string paymentIntentId)
        {
            try
            {
                var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(paymentIntentId);
                if (payment == null)
                    return ServiceResult<bool>.Failure("Payment record not found");

                payment.Status = "failed";
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Error handling payment failed: {ex.Message}");
            }
        }
    }
}
