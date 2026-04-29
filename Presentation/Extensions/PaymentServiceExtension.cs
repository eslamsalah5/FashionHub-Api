using Application.Services;
using Application.Services.Interfaces;
using Infrastructure.ExternalServices.PaymentGateways;
using Stripe;

namespace Presentation.Extensions
{
    public static class PaymentServiceExtension
    {
        public static IServiceCollection AddPaymentService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ── Stripe ────────────────────────────────────────────────────────
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];

            services.AddScoped<IPaymentGateway>(sp =>
                new StripePaymentGateway(
                    sp.GetRequiredService<PaymentIntentService>(),
                    configuration["Stripe:WebhookSecret"] ?? string.Empty));

            services.AddScoped<PaymentIntentService>();

            // ── Paymob ────────────────────────────────────────────────────────
            services.AddHttpClient(nameof(PaymobPaymentGateway));

            services.AddScoped<IPaymentGateway>(sp =>
            {
                var methodsSection = configuration.GetSection("Paymob:PaymentMethods");
                var methods = new Dictionary<string, PaymobMethodConfig>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var child in methodsSection.GetChildren())
                {
                    methods[child.Key] = new PaymobMethodConfig
                    {
                        IntegrationId = child["IntegrationId"] ?? string.Empty,
                        IframeId      = child["IframeId"]      ?? string.Empty
                    };
                }

                return new PaymobPaymentGateway(
                    secretKey:     configuration["Paymob:SecretKey"]     ?? string.Empty,
                    publicKey:     configuration["Paymob:PublicKey"]     ?? string.Empty,
                    hmacSecret:    configuration["Paymob:HmacSecret"]    ?? string.Empty,
                    defaultMethod: configuration["Paymob:DefaultMethod"] ?? "card",
                    methods:       methods,
                    httpClient:    sp.GetRequiredService<IHttpClientFactory>()
                                     .CreateClient(nameof(PaymobPaymentGateway)));
            });

            // ── Add more gateways here when needed ────────────────────────────
            // Example:
            //   services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();

            // ── Core payment service ──────────────────────────────────────────
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}
