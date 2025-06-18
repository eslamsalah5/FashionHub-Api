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

        public async Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(CreatePaymentIntentDto dto, string customerId)
        {
            try
            {
                // Get cart with items
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customerId);
                if (cart == null)
                {
                    return ServiceResult<PaymentIntentResponseDto>.Failure("Cart not found");
                }

                if (!cart.CartItems.Any())
                {
                    return ServiceResult<PaymentIntentResponseDto>.Failure("Cart is empty");
                }

                // Calculate total amount
                decimal totalAmount = cart.CartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

                // Create Stripe Payment Intent
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(totalAmount * 100), // Convert to cents
                    Currency = "usd",
                    PaymentMethodTypes = new List<string> { "card" },
                };

                var paymentIntent = await _paymentIntentService.CreateAsync(options);

                // Create payment record
                var payment = new Payment
                {
                    Amount = totalAmount,
                    StripePaymentIntentId = paymentIntent.Id,
                    Status = "pending"
                };

                await _unitOfWork.Payments.AddAsync(payment);
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<PaymentIntentResponseDto>.Success(new PaymentIntentResponseDto
                {
                    ClientSecret = paymentIntent.ClientSecret,
                    Amount = totalAmount
                });
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentIntentResponseDto>.Failure($"Error creating payment intent: {ex.Message}");
            }
        }

        public async Task<ServiceResult<int>> ConfirmPaymentAndCreateOrderAsync(ConfirmPaymentDto dto, string customerId)
        {
            try
            {
                // Get payment record
                var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(dto.PaymentIntentId);
                if (payment == null)
                {
                    return ServiceResult<int>.Failure("Payment not found");
                }

                // Retrieve payment intent from Stripe
                var paymentIntent = await _paymentIntentService.GetAsync(dto.PaymentIntentId);
                
                if (paymentIntent.Status != "succeeded")
                {
                    return ServiceResult<int>.Failure("Payment not successful");
                }

                // Update payment status
                payment.Status = "succeeded";
                payment.PaymentDate = DateTime.UtcNow;

                // Get customer's cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customerId);
                if (cart == null || !cart.CartItems.Any())
                {
                    return ServiceResult<int>.Failure("Cart not found or empty");
                }

                // Create order
                var order = new Order
                {
                    CustomerId = customerId,
                    TotalAmount = payment.Amount,
                    Status = OrderStatus.Processing,
                    Payment = payment
                };                // Create order items from cart items
                foreach (var cartItem in cart.CartItems)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = cartItem.ProductId,
                        ProductName = cartItem.Product.Name,
                        ProductSKU = cartItem.Product.SKU,
                        UnitPrice = cartItem.PriceAtAddition,
                        Quantity = cartItem.Quantity,
                        Subtotal = cartItem.Quantity * cartItem.PriceAtAddition,
                        SelectedSize = cartItem.SelectedSize,
                        SelectedColor = cartItem.SelectedColor
                    });
                }

                await _unitOfWork.Orders.AddAsync(order);

                // Clear cart
                await _unitOfWork.Carts.ClearCartAsync(cart.Id);

                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<int>.Success(order.Id);
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.Failure($"Error confirming payment: {ex.Message}");
            }
        }
    }
}
