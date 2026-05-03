using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Presentation.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}: {Message}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    ex.Message);

                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // Map known exception types to appropriate HTTP status codes
            var (statusCode, title) = exception switch
            {
                ValidationException  => (HttpStatusCode.BadRequest,            "Validation Error"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized,   "Unauthorized"),
                KeyNotFoundException => (HttpStatusCode.NotFound,              "Resource Not Found"),
                ArgumentNullException or ArgumentException
                                     => (HttpStatusCode.BadRequest,            "Bad Request"),
                NotImplementedException => (HttpStatusCode.NotImplemented,     "Not Implemented"),
                OperationCanceledException => (HttpStatusCode.ServiceUnavailable, "Request Cancelled"),
                _                    => (HttpStatusCode.InternalServerError,   "Internal Server Error")
            };

            context.Response.StatusCode = (int)statusCode;

#if DEBUG
            // In development, include the full exception details
            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Title      = title,
                Detail     = exception.Message,
                StackTrace = exception.StackTrace
            };
#else
            // In production, hide implementation details
            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Title      = title,
                Detail     = statusCode == HttpStatusCode.InternalServerError
                             ? "An unexpected error occurred. Please try again later."
                             : exception.Message
            };
#endif

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}
