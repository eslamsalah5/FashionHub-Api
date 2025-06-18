using Domain.Entities;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Payment?> GetByPaymentIntentIdAsync(string paymentIntentId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
        }
    }
}