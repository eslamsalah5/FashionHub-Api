using Application.DTOs.Payment;
using Application.Models;

namespace Application.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResult<PaymentIntentResponseDto>> CreatePaymentIntentAsync(CreatePaymentIntentDto dto, string customerId);
        
        Task<ServiceResult<int>> ConfirmPaymentAndCreateOrderAsync(ConfirmPaymentDto dto, string customerId);
    }
}
