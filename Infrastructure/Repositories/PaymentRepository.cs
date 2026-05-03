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

        public async Task<Payment?> GetByGatewayPaymentIdAsync(string gatewayPaymentId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(p => p.GatewayPaymentId == gatewayPaymentId);
        }

        /// <inheritdoc/>
        public async Task<Payment?> GetPendingByCustomerIdAsync(string customerId)
        {
            return await _dbSet
                .Where(p => p.CustomerId == customerId && p.Status == "pending")
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
        }
    }
}
