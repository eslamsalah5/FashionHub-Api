using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Infrastructure.ExternalServices.PaymentGateways
{
    /// <summary>
    /// Paymob Unified Checkout implementation of <see cref="IPaymentGateway"/>.
    ///
    /// Uses the modern Paymob API (v1/intention) — a single server-side call
    /// that returns a client_secret the frontend uses to open the hosted checkout.
    ///
    /// Server-side flow (1 step):
    ///   POST https://accept.paymob.com/v1/intention/
    ///   Headers: Authorization: Token {SecretKey}
    ///   Body: { amount, currency, payment_methods, billing_data, ... }
    ///   Response: { client_secret }
    ///
    /// Frontend redirects to:
    ///   https://accept.paymob.com/unifiedcheckout/?publicKey={PublicKey}&clientSecret={client_secret}
    ///
    /// Webhook verification uses HMAC-SHA512 over specific transaction fields.
    ///
    /// Supported payment methods (configured in dashboard):
    ///   "card"          — Visa / Mastercard / Meeza (Integration ID: Online Card)
    ///   "wallet"        — Vodafone Cash, Orange Cash, e& money (Integration ID: Mobile Wallet)
    /// </summary>
    public class PaymobPaymentGateway : IPaymentGateway
    {
        private readonly string _secretKey;
        private readonly string _publicKey;
        private readonly string _hmacSecret;
        private readonly string _defaultMethod;
        private readonly IReadOnlyDictionary<string, PaymobMethodConfig> _methods;
        private readonly HttpClient _httpClient;

        private const string BaseUrl = "https://accept.paymob.com";

        public string GatewayName => "paymob";

        public PaymobPaymentGateway(
            string secretKey,
            string publicKey,
            string hmacSecret,
            string defaultMethod,
            IReadOnlyDictionary<string, PaymobMethodConfig> methods,
            HttpClient httpClient)
        {
            _secretKey     = secretKey;
            _publicKey     = publicKey;
            _hmacSecret    = hmacSecret;
            _defaultMethod = defaultMethod;
            _methods       = methods;
            _httpClient    = httpClient;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CreateSessionAsync — single API call to /v1/intention/
        // Returns client_secret + publicKey for the frontend to open checkout
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ServiceResult<GatewaySessionResult>> CreateSessionAsync(
            decimal amount, string currency, string customerId,
            string? paymentMethod = null,
            CustomerBillingInfo? billingInfo = null)
        {
            var methodKey = (paymentMethod ?? _defaultMethod).ToLowerInvariant();

            if (!_methods.TryGetValue(methodKey, out var methodConfig))
                return ServiceResult<GatewaySessionResult>.Failure(
                    $"Paymob payment method '{methodKey}' is not configured. " +
                    $"Available: {string.Join(", ", _methods.Keys)}");

            if (string.IsNullOrEmpty(_secretKey))
                return ServiceResult<GatewaySessionResult>.Failure(
                    "Paymob SecretKey is not configured.");

            try
            {
                var amountCents = (long)(amount * 100);

                // ── Billing data — use real info if provided, safe defaults otherwise ──
                var firstName   = billingInfo?.FirstName.Trim()    is { Length: > 0 } fn ? fn : "Customer";
                var lastName    = billingInfo?.LastName.Trim()     is { Length: > 0 } ln ? ln : customerId;
                var email       = billingInfo?.Email.Trim()        is { Length: > 0 } em ? em : "customer@fashionhub.com";
                var phone       = billingInfo?.PhoneNumber.Trim()  is { Length: > 0 } ph ? ph : "+201000000000";
                var street      = billingInfo?.Address.Trim()      is { Length: > 0 } ad ? ad : "NA";
                var city        = billingInfo?.City.Trim()         is { Length: > 0 } ct ? ct : "Cairo";
                var country     = billingInfo?.Country.Trim()      is { Length: > 0 } co ? co : "EG";

                var intentionBody = new
                {
                    amount          = amountCents,
                    currency        = currency.ToUpperInvariant(),
                    // ── REQUIRED: Integration IDs from the Paymob dashboard ──
                    // Paymob returns 400 if this field is missing.
                    payment_methods = new[] { int.Parse(methodConfig.IntegrationId) },
                    items           = Array.Empty<object>(),
                    billing_data    = new
                    {
                        first_name   = firstName,
                        last_name    = lastName,
                        email        = email,
                        phone_number = phone,
                        apartment    = "NA",
                        floor        = "NA",
                        street       = street,
                        building     = "NA",
                        postal_code  = "NA",
                        city         = city,
                        country      = country,
                        state        = "NA"
                    },
                    customer = new
                    {
                        first_name   = firstName,
                        last_name    = lastName,
                        email        = email,
                        phone_number = phone
                    },
                    // Store customerId in extras so webhook can fall back to it
                    extras = new { customer_id = customerId }
                };

                var json = JsonSerializer.Serialize(intentionBody);
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, $"{BaseUrl}/v1/intention/");

                request.Headers.Add("Authorization", $"Token {_secretKey}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return ServiceResult<GatewaySessionResult>.Failure(
                        $"Paymob API error ({(int)response.StatusCode}): {body}");

                using var doc = JsonDocument.Parse(body);
                var clientSecret = doc.RootElement
                    .GetProperty("client_secret").GetString()
                    ?? throw new InvalidOperationException(
                        "client_secret not found in Paymob response.");

                // GatewayPaymentId = the intention ID echoed back as merchant_order_id in the webhook
                var intentionId = doc.RootElement.TryGetProperty("id", out var idProp)
                    ? idProp.ToString()
                    : clientSecret;

                return ServiceResult<GatewaySessionResult>.Success(new GatewaySessionResult
                {
                    ClientSecret     = clientSecret,
                    GatewayPaymentId = intentionId,
                    Amount           = amount,
                    PublicKey        = _publicKey   // frontend needs this for Unified Checkout
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
        //
        // Paymob sends POST to webhook URL with JSON body containing:
        //   { "type": "TRANSACTION", "obj": { ... transaction fields ... } }
        // The HMAC is sent as a query parameter: ?hmac=...
        // OR as a field in the body depending on the integration type.
        // We support both.
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

                // Get the transaction object — could be under "obj" or at root
                JsonElement obj;
                if (root.TryGetProperty("obj", out var objProp))
                    obj = objProp;
                else
                    obj = root;

                // Get HMAC — check body first, then headers
                var receivedHmac = string.Empty;
                if (root.TryGetProperty("hmac", out var hmacProp))
                    receivedHmac = hmacProp.GetString() ?? string.Empty;
                else if (headers.TryGetValue("hmac", out var headerHmac))
                    receivedHmac = headerHmac;

                // Verify HMAC if present
                if (!string.IsNullOrEmpty(receivedHmac))
                {
                    var computedHmac = ComputeHmac(obj);
                    if (!string.Equals(receivedHmac, computedHmac,
                            StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult(
                            ServiceResult<GatewayWebhookEvent>.Failure(
                                "Webhook verification failed: HMAC mismatch."));
                }

                // Parse transaction result
                var success = obj.TryGetProperty("success", out var sp) && sp.GetBoolean();
                var pending = obj.TryGetProperty("pending", out var pp) && pp.GetBoolean();

                // ─────────────────────────────────────────────────────
                // GatewayPaymentId resolution:
                // At intent creation we store the Paymob intention "id".
                // In the webhook, Paymob echoes that same id back as
                // order.merchant_order_id — use that for the DB lookup.
                // Fallback chain:
                //   1. obj.order.merchant_order_id  ← matches stored intention id
                //   2. obj.payment_key_claims.bill_reference
                //   3. obj.order.id                 ← Paymob internal order id (different)
                // ─────────────────────────────────────────────────────
                var gatewayPaymentId = string.Empty;

                if (obj.TryGetProperty("order", out var orderProp))
                {
                    // 1. merchant_order_id = the id we sent = intention id
                    if (orderProp.TryGetProperty("merchant_order_id", out var moidProp) &&
                        moidProp.ValueKind != JsonValueKind.Null)
                    {
                        gatewayPaymentId = moidProp.ToString();
                    }

                    // 2. Fallback: raw order id (may differ, but better than nothing)
                    if (string.IsNullOrEmpty(gatewayPaymentId) &&
                        orderProp.TryGetProperty("id", out var orderIdProp))
                    {
                        gatewayPaymentId = orderIdProp.ToString();
                    }
                }

                // 3. bill_reference from payment_key_claims (another echo of merchant_order_id)
                if (string.IsNullOrEmpty(gatewayPaymentId) &&
                    obj.TryGetProperty("payment_key_claims", out var claimsProp) &&
                    claimsProp.TryGetProperty("bill_reference", out var billRef))
                {
                    gatewayPaymentId = billRef.ToString();
                }

                // Extract customerId from extras so PaymentService can fall back to it
                var customerId = string.Empty;
                if (obj.TryGetProperty("order", out var orderForExtras) &&
                    orderForExtras.TryGetProperty("merchant_extra", out var extraProp) &&
                    extraProp.TryGetProperty("customer_id", out var cidProp))
                {
                    customerId = cidProp.GetString() ?? string.Empty;
                }

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
                        CustomerId       = customerId,
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
        // HMAC-SHA512 computation over required Paymob transaction fields
        // ─────────────────────────────────────────────────────────────────────
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
    /// Config for a single Paymob payment method.
    /// IntegrationId is the ID from the Paymob dashboard Payment Integrations.
    /// </summary>
    public class PaymobMethodConfig
    {
        public string IntegrationId { get; set; } = string.Empty;
        /// <summary>Not used in the new Unified Checkout API — kept for backward compat.</summary>
        public string IframeId { get; set; } = string.Empty;
    }
}
