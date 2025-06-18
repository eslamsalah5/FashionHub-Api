using Application.DTOs.Payment;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentDto dto)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found");
            }

            var result = await _paymentService.CreatePaymentIntentAsync(dto, customerId);

            if (result.IsSuccess)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentDto dto)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found");
            }

            var result = await _paymentService.ConfirmPaymentAndCreateOrderAsync(dto, customerId);

            if (result.IsSuccess)
            {
                return Ok(new { OrderId = result.Data, Message = "Payment confirmed and order created successfully" });
            }

            return BadRequest(result);
        }
    }
}
