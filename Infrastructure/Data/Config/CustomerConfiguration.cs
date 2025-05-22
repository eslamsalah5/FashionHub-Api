using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            // Configure one-to-one relationship with AppUser
            builder.HasOne(p => p.AppUser)
                   .WithOne(u => u.Customer)
                   .HasForeignKey<Customer>(p => p.Id)
                   .IsRequired()
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}