using Domain.Entities;

namespace Domain.Repositories.Interfaces
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        Task<Payment?> GetByPaymentIntentIdAsync(string paymentIntentId);
    }
}
