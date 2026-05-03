using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;

namespace Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEnumerable<IPaymentGateway> _gateways;

        public PaymentService(IUnitOfWork unitOfWork, IEnumerable<IPaymentGateway> gateways)
        {
            _unitOfWork = unitOfWork;
            _gateways   = gateways;
        }

        // ─────────────────────────────────────────────────────────────
        // Step 1 – Client calls this to start a payment session.
        // The gateway is chosen by the client (dto.Gateway).
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(
            CreatePaymentIntentDto dto, string customerId)
        {
            // Resolve the requested gateway
            var gateway = _gateways.FirstOrDefault(
                g => g.GatewayName.Equals(dto.Gateway, StringComparison.OrdinalIgnoreCase));

            if (gateway == null)
                return ServiceResult<PaymentIntentResponseDto>.Failure(
                    $"Payment gateway '{dto.Gateway}' is not supported.");

            // Validate cart
            var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customerId);
            if (cart == null)
                return ServiceResult<PaymentIntentResponseDto>.Failure("Cart not found");

            if (!cart.CartItems.Any())
                return ServiceResult<PaymentIntentResponseDto>.Failure("Cart is empty");

            decimal totalAmount = cart.CartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

            // Build real billing info from the customer's profile
            var billingInfo = await BuildBillingInfoAsync(customerId);

            // Delegate to the chosen gateway
            var sessionResult = await gateway.CreateSessionAsync(
                totalAmount, "EGP", customerId, dto.PaymentMethod, billingInfo);

            if (!sessionResult.IsSuccess)
                return ServiceResult<PaymentIntentResponseDto>.Failure(sessionResult.Errors);

            // Persist a pending payment record
            var payment = new Payment
            {
                Amount           = totalAmount,
                GatewayPaymentId = sessionResult.Data!.GatewayPaymentId,
                GatewayName      = gateway.GatewayName,
                Status           = "pending",
                CustomerId       = customerId
            };

            await _unitOfWork.Payments.AddAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            // Build redirect URL for Paymob
            string? redirectUrl = null;
            if (gateway.GatewayName.Equals("paymob", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(sessionResult.Data.PublicKey)
                && !string.IsNullOrEmpty(sessionResult.Data.ClientSecret))
            {
                redirectUrl = $"https://accept.paymob.com/unifiedcheckout/?publicKey={sessionResult.Data.PublicKey}&clientSecret={sessionResult.Data.ClientSecret}";
            }

            return ServiceResult<PaymentIntentResponseDto>.Success(new PaymentIntentResponseDto
            {
                ClientSecret = sessionResult.Data.ClientSecret,
                Amount       = totalAmount,
                PublicKey    = sessionResult.Data.PublicKey,
                Gateway      = gateway.GatewayName,
                RedirectUrl  = redirectUrl
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Helper: fetch real customer billing info for Paymob requests
        // ─────────────────────────────────────────────────────────────
        private async Task<CustomerBillingInfo> BuildBillingInfoAsync(string customerId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(customerId);
                if (user == null) return new CustomerBillingInfo();

                // Split FullName into first / last
                var parts     = (user.FullName ?? string.Empty).Trim().Split(' ', 2);
                var firstName = parts.Length > 0 ? parts[0] : string.Empty;
                var lastName  = parts.Length > 1 ? parts[1] : string.Empty;

                return new CustomerBillingInfo
                {
                    FirstName   = firstName,
                    LastName    = lastName,
                    Email       = user.Email       ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    Address     = user.Address     ?? string.Empty,
                };
            }
            catch
            {
                // Non-fatal — return defaults if lookup fails
                return new CustomerBillingInfo();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Step 2 – Called by the webhook handler when payment succeeds.
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<int>> HandlePaymentSucceededAsync(GatewayWebhookEvent webhookEvent)
        {
            try
            {
                // Primary lookup: by GatewayPaymentId stored at intent creation
                var payment = await _unitOfWork.Payments.GetByGatewayPaymentIdAsync(
                    webhookEvent.GatewayPaymentId);

                // Fallback lookup: latest pending payment for this customer
                // (needed for Paymob when merchant_order_id ≠ stored intention id)
                if (payment == null && !string.IsNullOrEmpty(webhookEvent.CustomerId))
                {
                    payment = await _unitOfWork.Payments
                        .GetPendingByCustomerIdAsync(webhookEvent.CustomerId);
                }

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
                        ProductId     = cartItem.ProductId,
                        ProductName   = cartItem.Product.Name,
                        ProductSKU    = cartItem.Product.SKU,
                        UnitPrice     = cartItem.PriceAtAddition,
                        Quantity      = cartItem.Quantity,
                        Subtotal      = cartItem.Quantity * cartItem.PriceAtAddition,
                        SelectedSize  = cartItem.SelectedSize,
                        SelectedColor = cartItem.SelectedColor
                    });

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
        // Step 3 – Called by the webhook handler when payment fails.
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<bool>> HandlePaymentFailedAsync(GatewayWebhookEvent webhookEvent)
        {
            try
            {
                var payment = await _unitOfWork.Payments.GetByGatewayPaymentIdAsync(
                    webhookEvent.GatewayPaymentId);

                // Fallback: find by customerId if gatewayPaymentId doesn't match
                if (payment == null && !string.IsNullOrEmpty(webhookEvent.CustomerId))
                {
                    payment = await _unitOfWork.Payments
                        .GetPendingByCustomerIdAsync(webhookEvent.CustomerId);
                }

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
