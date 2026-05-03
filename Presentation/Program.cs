using Presentation.Extensions;
using Presentation.BackgroundServices;

namespace Presentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.ConfigureSerilog();

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddMemoryCache();

            // Enable Gzip/Brotli compression for best performance
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/octet-stream", "image/svg+xml" });
            });

            builder.Services.AddRepositoryServices();


            // Configure Email services using extension method
            builder.Services.AddEmailServices(builder.Configuration);


            // Configure Identity services (must come before JWT services)
            builder.Services.AddIdentityServices(builder.Configuration);


            // Configure File services (must be registered before Auth service)
            builder.Services.AddFileService();

            // Configure Swagger/OpenAPI using extension method
            builder.Services.AddSwaggerServices();

            // Configure CORS using extension method
            builder.Services.AddCorsServices(builder.Configuration);


            // Configure JWT services
            builder.Services.AddJwtServices(builder.Configuration);
              // Configure Auth services
            builder.Services.AddAuthService();
              // Configure Product services
            builder.Services.AddProductService();
              // Configure Cart services
            builder.Services.AddCartService();
              // Configure Order services
            builder.Services.AddOrderService();
              // Configure Payment services
            builder.Services.AddPaymentService(builder.Configuration);

              // Configure reservation expiry sweep
              builder.Services.Configure<PaymentReservationOptions>(
                builder.Configuration.GetSection("PaymentReservation"));
              builder.Services.AddHostedService<PaymentReservationExpiryService>();
                        

            // Configure Data Seed services
            builder.Services.AddDataSeedServices();

            var app = builder.Build();

            // Ensure Serilog is properly closed on application shutdown
            app.EnsureSerilogClosed();

            // Use Global Exception Handling Middleware
            app.UseMiddleware<Presentation.Middlewares.ExceptionMiddleware>();

            // Seed database data
            await app.SeedDatabaseAsync();

            // Enable request body buffering so the Stripe webhook can read the raw body
            // for signature verification (must be before routing/controllers)
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next();
            });

            // Configure the Swagger middleware using extension method
            app.UseSwaggerMiddleware();

            // Enable compression middleware
            app.UseResponseCompression();

            // Use CORS middleware (should be before authentication)
            app.UseCorsMiddleware();

            // Enable default static files (to serve /uploads and other root-level assets)
            app.UseStaticFiles();

            // Configure Static Files from the Angular 'browser' output folder (Production only)
            if (!app.Environment.IsDevelopment())
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "browser")),
                    OnPrepareResponse = ctx =>
                    {
                        // Cache static assets for 1 year in production
                        const int durationInSeconds = 60 * 60 * 24 * 365;
                        ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] =
                            "public,max-age=" + durationInSeconds;
                    }
                });
            }

            app.UseHttpsRedirection();

            // Add authentication middleware
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            
            // Map fallback for Angular client-side routing (Production only)
            if (!app.Environment.IsDevelopment())
            {
                app.MapFallbackToFile("browser/index.html");
            }

            await app.RunAsync();
        }
    }
}
