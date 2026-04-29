using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Infrastructure.ExternalServices.PaymentGateways
{
    /// <summary>
    /// Paymob (Accept) implementation of <see cref="IPaymentGateway"/>.
    ///
    /// Supports multiple payment methods — each has its own IntegrationId + IframeId
    /// configured in appsettings.json under Paymob:PaymentMethods.
    ///
    /// Supported methods (configured in dashboard):
    ///   "card"          — Visa / Mastercard / Meeza
    ///   "vodafone_cash" — Vodafone Cash
    ///   "orange_cash"   — Orange Cash
    ///   "fawry"         — Fawry
    ///   "wallet"        — Mobile wallets (e& money, We Pay, etc.)
    ///
    /// Server-side flow (3 steps before frontend sees anything):
    ///   Step 1 — POST /api/auth/tokens           → auth_token
    ///   Step 2 — POST /api/ecommerce/orders       → order_id
    ///   Step 3 — POST /api/acceptance/payment_keys → payment_key
    ///
    /// Frontend renders:
    ///   https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={payment_key}
    ///
    /// Webhook verification uses HMAC-SHA512 over specific transaction fields.
    /// </summary>
    public class PaymobPaymentGateway : IPaymentGateway
    {
        private readonly string _apiKey;
        private readonly string _hmacSecret;
        private readonly string _defaultMethod;
        private readonly IReadOnlyDictionary<string, PaymobMethodConfig> _methods;
        private readonly HttpClient _httpClient;

        private const string BaseUrl = "https://accept.paymob.com/api";

        public string GatewayName => "paymob";

        public PaymobPaymentGateway(
            string apiKey,
            string hmacSecret,
            string defaultMethod,
            IReadOnlyDictionary<string, PaymobMethodConfig> methods,
            HttpClient httpClient)
        {
            _apiKey        = apiKey;
            _hmacSecret    = hmacSecret;
            _defaultMethod = defaultMethod;
            _methods       = methods;
            _httpClient    = httpClient;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CreateSessionAsync
        // paymentMethod: "card" | "vodafone_cash" | "orange_cash" | "fawry" | "wallet"
        // Falls back to the default method if null.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ServiceResult<GatewaySessionResult>> CreateSessionAsync(
            decimal amount, string currency, string customerId, string? paymentMethod = null)
        {
            // Resolve which method config to use
            var methodKey = (paymentMethod ?? _defaultMethod).ToLowerInvariant();

            if (!_methods.TryGetValue(methodKey, out var methodConfig))
                return ServiceResult<GatewaySessionResult>.Failure(
                    $"Paymob payment method '{methodKey}' is not configured. " +
                    $"Available methods: {string.Join(", ", _methods.Keys)}");

            try
            {
                // ── Step 1: Authenticate ──────────────────────────────────────
                var authToken = await GetAuthTokenAsync();

                // ── Step 2: Register order ────────────────────────────────────
                var amountCents = (long)(amount * 100);
                var orderId = await RegisterOrderAsync(authToken, amountCents, currency);

                // ── Step 3: Get payment key ───────────────────────────────────
                var paymentKey = await GetPaymentKeyAsync(
                    authToken, orderId, amountCents, currency,
                    customerId, methodConfig.IntegrationId);

                return ServiceResult<GatewaySessionResult>.Success(new GatewaySessionResult
                {
                    ClientSecret     = paymentKey,
                    GatewayPaymentId = orderId.ToString(),
                    Amount           = amount,
                    IframeId         = methodConfig.IframeId
                });
            }
            catch (Exception ex)
            {
                return ServiceResult<GatewaySessionResult>.Failure(
                    $"Paymob error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ParseWebhookAsync — HMAC-SHA512 verification
        // ─────────────────────────────────────────────────────────────────────
        public Task<ServiceResult<GatewayWebhookEvent>> ParseWebhookAsync(
            string rawBody, IDictionary<string, string> headers)
        {
            if (string.IsNullOrEmpty(_hmacSecret))
                return Task.FromResult(
                    ServiceResult<GatewayWebhookEvent>.Failure(
                        "Paymob HMAC secret is not configured."));

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                if (!root.TryGetProperty("hmac", out var hmacElement))
                    return Task.FromResult(
                        ServiceResult<GatewayWebhookEvent>.Failure(
                            "Webhook verification failed: hmac field missing."));

                var receivedHmac = hmacElement.GetString() ?? string.Empty;

                if (!root.TryGetProperty("obj", out var obj))
                    return Task.FromResult(
                        ServiceResult<GatewayWebhookEvent>.Failure(
                            "Webhook verification failed: obj field missing."));

                var computedHmac = ComputeHmac(obj);

                if (!string.Equals(receivedHmac, computedHmac, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(
                        ServiceResult<GatewayWebhookEvent>.Failure(
                            "Webhook verification failed: HMAC mismatch."));

                // Parse result
                var success = obj.TryGetProperty("success", out var sp) && sp.GetBoolean();
                var pending = obj.TryGetProperty("pending", out var pp) && pp.GetBoolean();

                var gatewayPaymentId = string.Empty;
                if (obj.TryGetProperty("order", out var orderProp) &&
                    orderProp.TryGetProperty("id", out var orderIdProp))
                    gatewayPaymentId = orderIdProp.ToString();

                var eventType = (success, pending) switch
                {
                    (true, false)  => GatewayEventTypes.PaymentSucceeded,
                    (false, false) => GatewayEventTypes.PaymentFailed,
                    _              => "payment.pending"
                };

                var eventId = obj.TryGetProperty("id", out var idProp)
                    ? idProp.ToString() : string.Empty;

                return Task.FromResult(ServiceResult<GatewayWebhookEvent>.Success(
                    new GatewayWebhookEvent
                    {
                        EventType        = eventType,
                        GatewayPaymentId = gatewayPaymentId,
                        EventId          = eventId
                    }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(
                    ServiceResult<GatewayWebhookEvent>.Failure(
                        $"Webhook parsing failed: {ex.Message}"));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> GetAuthTokenAsync()
        {
            var payload = JsonSerializer.Serialize(new { api_key = _apiKey });
            var response = await PostJsonAsync($"{BaseUrl}/auth/tokens", payload);
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("token").GetString()
                   ?? throw new InvalidOperationException("Paymob auth token not found.");
        }

        private async Task<long> RegisterOrderAsync(
            string authToken, long amountCents, string currency)
        {
            var payload = JsonSerializer.Serialize(new
            {
                auth_token      = authToken,
                delivery_needed = false,
                amount_cents    = amountCents.ToString(),
                currency        = currency.ToUpperInvariant(),
                items           = Array.Empty<object>()
            });
            var response = await PostJsonAsync($"{BaseUrl}/ecommerce/orders", payload);
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("id").GetInt64();
        }

        private async Task<string> GetPaymentKeyAsync(
            string authToken, long orderId, long amountCents,
            string currency, string customerId, string integrationId)
        {
            var payload = JsonSerializer.Serialize(new
            {
                auth_token           = authToken,
                amount_cents         = amountCents.ToString(),
                expiration           = 3600,
                order_id             = orderId,
                billing_data         = new
                {
                    first_name      = "Customer",
                    last_name       = customerId,
                    email           = "customer@fashionhub.com",
                    phone_number    = "+201000000000",
                    apartment       = "NA",
                    floor           = "NA",
                    street          = "NA",
                    building        = "NA",
                    shipping_method = "NA",
                    postal_code     = "NA",
                    city            = "Cairo",
                    country         = "EG",
                    state           = "NA"
                },
                currency             = currency.ToUpperInvariant(),
                integration_id       = int.Parse(integrationId),
                lock_order_when_paid = "false"
            });
            var response = await PostJsonAsync($"{BaseUrl}/acceptance/payment_keys", payload);
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("token").GetString()
                   ?? throw new InvalidOperationException("Paymob payment key not found.");
        }

        private async Task<string> PostJsonAsync(string url, string jsonPayload)
        {
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Paymob API error ({response.StatusCode}): {body}");
            return body;
        }

        private string ComputeHmac(JsonElement obj)
        {
            var fields = new[]
            {
                GetString(obj, "amount_cents"),
                GetString(obj, "created_at"),
                GetString(obj, "currency"),
                GetString(obj, "error_occured"),
                GetString(obj, "has_parent_transaction"),
                GetString(obj, "id"),
                GetString(obj, "integration_id"),
                GetString(obj, "is_3d_secure"),
                GetString(obj, "is_auth"),
                GetString(obj, "is_capture"),
                GetString(obj, "is_refunded"),
                GetString(obj, "is_standalone_payment"),
                GetString(obj, "is_voided"),
                GetNestedString(obj, "order", "id"),
                GetString(obj, "owner"),
                GetString(obj, "pending"),
                GetNestedString(obj, "source_data", "pan"),
                GetNestedString(obj, "source_data", "sub_type"),
                GetNestedString(obj, "source_data", "type"),
                GetString(obj, "success")
            };

            var concatenated = string.Concat(fields);
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_hmacSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string GetString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var prop)) return string.Empty;
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? string.Empty,
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                JsonValueKind.Null   => string.Empty,
                _                   => prop.ToString()
            };
        }

        private static string GetNestedString(JsonElement element, string parent, string child)
        {
            if (!element.TryGetProperty(parent, out var parentProp)) return string.Empty;
            return GetString(parentProp, child);
        }
    }

    /// <summary>
    /// Config for a single Paymob payment method (Integration + iFrame pair).
    /// </summary>
    public class PaymobMethodConfig
    {
        public string IntegrationId { get; set; } = string.Empty;
        public string IframeId      { get; set; } = string.Empty;
    }
}
