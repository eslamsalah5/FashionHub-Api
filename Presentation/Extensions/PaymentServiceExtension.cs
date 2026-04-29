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

            // ── Add more gateways here when needed ────────────────────────────
            // Example (PayPal):
            //   services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();
            //
            // Example (Paymob):
            //   services.AddScoped<IPaymentGateway, PaymobPaymentGateway>();

            // ── Core payment service ──────────────────────────────────────────
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}
