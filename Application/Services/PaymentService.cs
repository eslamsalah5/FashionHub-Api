using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using System.Text.Json;

namespace Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEnumerable<IPaymentGateway> _gateways;

        private sealed record CartSnapshotItem(
            int ProductId,
            string ProductName,
            string ProductSKU,
            decimal UnitPrice,
            int Quantity,
            string SelectedSize,
            string SelectedColor);

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

            var snapshotItems = cart.CartItems.Select(ci => new CartSnapshotItem(
                ProductId: ci.ProductId,
                ProductName: ci.Product.Name,
                ProductSKU: ci.Product.SKU,
                UnitPrice: ci.PriceAtAddition,
                Quantity: ci.Quantity,
                SelectedSize: ci.SelectedSize,
                SelectedColor: ci.SelectedColor
            )).ToList();

            // Build real billing info from the customer's profile
            var billingInfo = await BuildBillingInfoAsync(customerId);

            // Delegate to the chosen gateway
            var sessionResult = await gateway.CreateSessionAsync(
                totalAmount, "EGP", customerId, dto.PaymentMethod, billingInfo);

            if (!sessionResult.IsSuccess)
                return ServiceResult<PaymentIntentResponseDto>.Failure(sessionResult.Errors);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var cartItem in cart.CartItems)
                {
                    if (cartItem.Product.StockQuantity < cartItem.Quantity)
                        return await RollbackAndFailAsync<PaymentIntentResponseDto>(
                            $"Insufficient stock for product '{cartItem.Product.Name}'.");

                    cartItem.Product.StockQuantity -= cartItem.Quantity;
                }

                // Persist a pending payment record
                var payment = new Payment
                {
                    Amount           = totalAmount,
                    CartSnapshotJson = JsonSerializer.Serialize(snapshotItems),
                    GatewayPaymentId = sessionResult.Data!.GatewayPaymentId,
                    GatewayName      = gateway.GatewayName,
                    Status           = "pending",
                    CustomerId       = customerId
                };

                await _unitOfWork.Payments.AddAsync(payment);
                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await SafeRollbackAsync();
                return ServiceResult<PaymentIntentResponseDto>.Failure(
                    $"Failed to reserve stock: {ex.Message}");
            }

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

                await _unitOfWork.BeginTransactionAsync();

                var order = new Order
                {
                    CustomerId = payment.CustomerId,
                    Status     = OrderStatus.Processing,
                    Payment    = payment
                };

                decimal totalAmount = 0m;
                var snapshotItems = TryReadSnapshot(payment.CartSnapshotJson);

                if (snapshotItems is { Count: > 0 })
                {
                    foreach (var item in snapshotItems)
                    {
                        order.OrderItems.Add(new OrderItem
                        {
                            ProductId     = item.ProductId,
                            ProductName   = item.ProductName,
                            ProductSKU    = item.ProductSKU,
                            UnitPrice     = item.UnitPrice,
                            Quantity      = item.Quantity,
                            Subtotal      = item.UnitPrice * item.Quantity,
                            SelectedSize  = item.SelectedSize,
                            SelectedColor = item.SelectedColor
                        });

                        totalAmount += item.UnitPrice * item.Quantity;
                    }
                }
                else
                {
                    var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(payment.CustomerId);
                    if (cart == null || !cart.CartItems.Any())
                        return await RollbackAndFailAsync<int>("Cart not found or already cleared");

                    foreach (var cartItem in cart.CartItems)
                    {
                        if (cartItem.Product.StockQuantity < cartItem.Quantity)
                            return await RollbackAndFailAsync<int>(
                                $"Insufficient stock for product '{cartItem.Product.Name}'.");

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

                        totalAmount += cartItem.PriceAtAddition * cartItem.Quantity;
                    }

                    await _unitOfWork.Carts.ClearCartAsync(cart.Id);
                }

                order.TotalAmount = totalAmount;
                payment.Status = "succeeded";
                payment.PaymentDate = DateTime.UtcNow;

                await _unitOfWork.Orders.AddAsync(order);

                // Best-effort cart cleanup even when snapshot was used
                var existingCart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(payment.CustomerId);
                if (existingCart != null && existingCart.CartItems.Any())
                {
                    await _unitOfWork.Carts.ClearCartAsync(existingCart.Id);
                }

                await _unitOfWork.CommitTransactionAsync();

                return ServiceResult<int>.Success(order.Id);
            }
            catch (Exception ex)
            {
                await SafeRollbackAsync();
                return ServiceResult<int>.Failure($"Error handling payment succeeded: {ex.Message}");
            }
        }

        private static List<CartSnapshotItem>? TryReadSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<List<CartSnapshotItem>>(json);
            }
            catch
            {
                return null;
            }
        }

        private async Task<ServiceResult<T>> RollbackAndFailAsync<T>(string message)
        {
            await SafeRollbackAsync();
            return ServiceResult<T>.Failure(message);
        }

        private async Task SafeRollbackAsync()
        {
            try
            {
                await _unitOfWork.RollbackTransactionAsync();
            }
            catch
            {
                // Swallow rollback errors to avoid masking the original failure.
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

                await _unitOfWork.BeginTransactionAsync();

                var snapshotItems = TryReadSnapshot(payment.CartSnapshotJson);
                if (snapshotItems is { Count: > 0 })
                {
                    foreach (var item in snapshotItems)
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        if (product != null)
                            product.StockQuantity += item.Quantity;
                    }
                }

                payment.Status = "failed";
                await _unitOfWork.CommitTransactionAsync();

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                await SafeRollbackAsync();
                return ServiceResult<bool>.Failure($"Error handling payment failed: {ex.Message}");
            }
        }
    }
}
