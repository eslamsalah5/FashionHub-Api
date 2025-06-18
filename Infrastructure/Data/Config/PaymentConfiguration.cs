using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.HasKey(p => p.Id);
            
            builder.Property(p => p.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");
                
            builder.Property(p => p.StripePaymentIntentId)
                .IsRequired()
                .HasMaxLength(255);
                
            builder.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(50);
                
            builder.Property(p => p.PaymentDate)
                .IsRequired();
                
            // Create unique index on StripePaymentIntentId
            builder.HasIndex(p => p.StripePaymentIntentId)
                .IsUnique();
        }
    }
}
